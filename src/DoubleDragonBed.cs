// Potato Tweaks - double dragon bed.
// Ported from Double Dragon Bed 1.0.3 by Hansenbansen (hansenbansen.doublebed), MIT License,
// Copyright (c) 2026 Hansenbansen. See LICENSE. Changes: runs inside Potato Tweaks (its own version handshake is
// replaced by Potato Tweaks' network check), private game members are reached through AccessTools, and the
// blanket setting lives in potato.tweaks.cfg. Beds keep the same saved data, so beds claimed with the original mod
// keep their owners.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoTweaks.DoubleBed
{
	// Private game members the bed code uses.
	internal static class Access
	{
		internal static readonly AccessTools.FieldRef<Bed, ZNetView> BedView = AccessTools.FieldRefAccess<Bed, ZNetView>("m_nview");
		internal static readonly AccessTools.FieldRef<WearNTear, ZNetView> WearView = AccessTools.FieldRefAccess<WearNTear, ZNetView>("m_nview");
		internal static readonly AccessTools.FieldRef<WearNTear, Collider[]> WearColliders = AccessTools.FieldRefAccess<WearNTear, Collider[]>("m_colliders");
		internal static readonly AccessTools.FieldRef<Character, GameObject> Visual = AccessTools.FieldRefAccess<Character, GameObject>("m_visual");
		internal static readonly AccessTools.FieldRef<Humanoid, VisEquipment> VisEquip = AccessTools.FieldRefAccess<Humanoid, VisEquipment>("m_visEquipment");
		internal static readonly AccessTools.FieldRef<VisEquipment, GameObject> HairInstance = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_hairItemInstance");
		internal static readonly AccessTools.FieldRef<VisEquipment, GameObject> BeardInstance = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_beardItemInstance");
		internal static readonly AccessTools.FieldRef<VisEquipment, GameObject> HelmetInstance = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_helmetItemInstance");
		internal static readonly Func<Bed, bool> IsMine = AccessTools.MethodDelegate<Func<Bed, bool>>(AccessTools.Method(typeof(Bed), "IsMine"));
		internal static readonly Action<Bed, long, string> SetOwner = AccessTools.MethodDelegate<Action<Bed, long, string>>(AccessTools.Method(typeof(Bed), "SetOwner"));
	}

	internal sealed class BedSide : MonoBehaviour
	{
		public GameObject Piece => base.transform.parent.gameObject;
	}
	internal static class BedSides
	{
		public const string Prefab = "piece_bed02";

		public const string SideName = "DoubleBed_side2";

		public const float Offset = 0.55f;

		public const string SideRpc = "DoubleBed_SetOwner";

		public static readonly int SideOwner = "DoubleBed_owner".GetStableHashCode();

		public static readonly int SideOwnerName = "DoubleBed_ownerName".GetStableHashCode();

		public const string MiddleName = "spawnpoint_both";

		public const string UnclaimText = "Unclaim";

		public const string UnclaimedText = "Bed unclaimed";

		public static int OwnerKey(Bed bed)
		{
			if (!IsSecond(bed))
			{
				return ZDOVars.s_owner;
			}
			return SideOwner;
		}

		public static int OwnerNameKey(Bed bed)
		{
			if (!IsSecond(bed))
			{
				return ZDOVars.s_ownerName;
			}
			return SideOwnerName;
		}

		public static string RpcName(Bed bed)
		{
			if (!IsSecond(bed))
			{
				return "SetOwner";
			}
			return "DoubleBed_SetOwner";
		}

		public static ZNetView NetViewOf(Component bed)
		{
			if (!bed.TryGetComponent<BedSide>(out var component))
			{
				return bed.GetComponent<ZNetView>();
			}
			return component.Piece.GetComponent<ZNetView>();
		}

		public static GameObject ColliderRoot(Component bed)
		{
			if (!bed.TryGetComponent<BedSide>(out var component))
			{
				return bed.gameObject;
			}
			return component.Piece;
		}

		public static bool IsSecond(Bed bed)
		{
			BedSide component;
			return bed.TryGetComponent<BedSide>(out component);
		}

		public static Bed OtherSide(Bed bed)
		{
			if (bed.TryGetComponent<BedSide>(out var component))
			{
				return component.Piece.GetComponent<Bed>();
			}
			return SecondSide(bed.gameObject);
		}

		public static bool IsDragonBedSide(Bed bed)
		{
			if (!IsSecond(bed))
			{
				return SecondSide(bed.gameObject) != null;
			}
			return true;
		}

		public static bool IsMyDragonSide(Bed bed)
		{
			if (Valid(bed) && Access.IsMine(bed))
			{
				return IsDragonBedSide(bed);
			}
			return false;
		}

		public static Bed FirstSide(Bed bed)
		{
			if (!IsSecond(bed))
			{
				return bed;
			}
			return OtherSide(bed);
		}

		public static bool BothMine(Bed bed)
		{
			Bed bed2 = OtherSide(bed);
			if (bed2 != null && Valid(bed) && Valid(bed2) && Access.IsMine(bed))
			{
				return Access.IsMine(bed2);
			}
			return false;
		}

		private static bool Valid(Bed bed)
		{
			if (Access.BedView(bed) != null)
			{
				return Access.BedView(bed).GetZDO() != null;
			}
			return false;
		}

		public static Transform AttachPoint(Bed bed)
		{
			if (!BothMine(bed))
			{
				return bed.m_spawnPoint;
			}
			Transform transform = FirstSide(bed).transform.Find("DoubleBed_side2/spawnpoint_both");
			if (!(transform != null))
			{
				return bed.m_spawnPoint;
			}
			return transform;
		}

		public static Vector3 SpawnPoint(Bed bed, Vector3 own)
		{
			if (bed.m_spawnPoint == null || !BothMine(bed))
			{
				return own;
			}
			Transform transform = FirstSide(bed).transform.Find("DoubleBed_side2/spawnpoint_both");
			if (!(transform != null))
			{
				return own;
			}
			return transform.position;
		}

		public static Bed SecondSide(GameObject piece)
		{
			Transform transform = piece.transform.Find("DoubleBed_side2");
			if (!(transform != null))
			{
				return null;
			}
			return transform.GetComponent<Bed>();
		}

		public static void Wake(Bed bed)
		{
			if (!IsSecond(bed) && !(Utils.GetPrefabName(bed.gameObject) != "piece_bed02"))
			{
				Apply(bed);
			}
		}

		public static void Install()
		{
			Bed[] array = UnityEngine.Object.FindObjectsByType<Bed>(FindObjectsSortMode.None);
			foreach (Bed bed in array)
			{
				if (bed != null && !IsSecond(bed) && Utils.GetPrefabName(bed.gameObject) == "piece_bed02")
				{
					Apply(bed);
				}
			}
		}

		public static void Uninstall()
		{
			GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab("piece_bed02") : null);
			BedSide[] array = UnityEngine.Object.FindObjectsByType<BedSide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (BedSide obj in array)
			{
				GameObject piece = obj.Piece;
				ZNetView component = piece.GetComponent<ZNetView>();
				if (component != null && component.GetZDO() != null)
				{
					component.Unregister("DoubleBed_SetOwner");
				}
				UnityEngine.Object.DestroyImmediate(obj.gameObject);
				if (gameObject != null)
				{
					Restore(piece, gameObject);
				}
			}
		}

		private static void Apply(Bed first)
		{
			GameObject gameObject = first.gameObject;
			if (gameObject.transform.Find("DoubleBed_side2") != null || first.m_spawnPoint == null)
			{
				return;
			}
			GameObject gameObject2 = new GameObject("DoubleBed_side2");
			gameObject2.SetActive(value: false);
			gameObject2.layer = gameObject.layer;
			gameObject2.tag = gameObject.tag;
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			BoxCollider[] componentsInChildren = gameObject.GetComponentsInChildren<BoxCollider>(includeInactive: true);
			foreach (BoxCollider boxCollider in componentsInChildren)
			{
				if (!boxCollider.isTrigger && Aligned(boxCollider.transform, gameObject.transform))
				{
					Vector3 vector = gameObject.transform.InverseTransformPoint(boxCollider.transform.TransformPoint(boxCollider.center));
					float num = vector.x - boxCollider.size.x * 0.5f;
					float num2 = vector.x + boxCollider.size.x * 0.5f;
					if (!(num >= 0f) && !(num2 <= 0f))
					{
						GameObject gameObject3 = new GameObject((boxCollider.gameObject == gameObject) ? "collider_side2" : boxCollider.gameObject.name);
						gameObject3.layer = boxCollider.gameObject.layer;
						gameObject3.tag = boxCollider.gameObject.tag;
						gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
						BoxCollider boxCollider2 = gameObject3.AddComponent<BoxCollider>();
						boxCollider2.center = new Vector3(num2 * 0.5f, vector.y, vector.z);
						boxCollider2.size = new Vector3(num2, boxCollider.size.y, boxCollider.size.z);
						boxCollider2.sharedMaterial = boxCollider.sharedMaterial;
						boxCollider.center += new Vector3(num * 0.5f - vector.x, 0f, 0f);
						boxCollider.size = new Vector3(0f - num, boxCollider.size.y, boxCollider.size.z);
					}
				}
			}
			Transform spawnPoint = first.m_spawnPoint;
			Vector3 position = spawnPoint.position;
			Vector3 vector2 = gameObject.transform.right * 0.55f;
			Transform transform = new GameObject("spawnpoint").transform;
			transform.SetParent(gameObject2.transform, worldPositionStays: false);
			transform.SetPositionAndRotation(position + vector2, spawnPoint.rotation);
			Transform transform2 = new GameObject("spawnpoint_both").transform;
			transform2.SetParent(gameObject2.transform, worldPositionStays: false);
			transform2.SetPositionAndRotation(position, spawnPoint.rotation);
			spawnPoint.position = position - vector2;
			Bed bed = gameObject2.AddComponent<Bed>();
			bed.m_spawnPoint = transform;
			bed.m_monsterCheckRadius = first.m_monsterCheckRadius;
			bed.m_hoverOffset = first.m_hoverOffset;
			gameObject2.AddComponent<BedSide>();
			ForgetColliders(gameObject);
			gameObject2.SetActive(value: true);
		}

		private static void Restore(GameObject piece, GameObject prefab)
		{
			BoxCollider[] componentsInChildren = piece.GetComponentsInChildren<BoxCollider>(includeInactive: true);
			BoxCollider[] componentsInChildren2 = prefab.GetComponentsInChildren<BoxCollider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length && i < componentsInChildren2.Length; i++)
			{
				componentsInChildren[i].center = componentsInChildren2[i].center;
				componentsInChildren[i].size = componentsInChildren2[i].size;
			}
			Bed component = piece.GetComponent<Bed>();
			Bed component2 = prefab.GetComponent<Bed>();
			if (component != null && component2 != null && component.m_spawnPoint != null && component2.m_spawnPoint != null)
			{
				component.m_spawnPoint.SetLocalPositionAndRotation(component2.m_spawnPoint.localPosition, component2.m_spawnPoint.localRotation);
			}
			ForgetColliders(piece);
		}

		private static void ForgetColliders(GameObject piece)
		{
			if (piece.TryGetComponent<WearNTear>(out var component))
			{
				Access.WearColliders(component) = null;
			}
		}

		private static bool Aligned(Transform t, Transform root)
		{
			while (t != root)
			{
				if (t == null)
				{
					return false;
				}
				if (Quaternion.Angle(t.localRotation, Quaternion.identity) > 0.1f)
				{
					return false;
				}
				if ((t.localScale - Vector3.one).sqrMagnitude > 1E-06f)
				{
					return false;
				}
				t = t.parent;
			}
			return true;
		}
	}
	internal sealed class Part
	{
		public readonly List<int> Triangles = new List<int>();

		public Vector3 Min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

		public Vector3 Max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

		public Vector3 Size => Max - Min;

		public Vector3 Centre => (Min + Max) * 0.5f;
	}
	internal sealed class BedParts
	{
		public readonly List<Part> Parts;

		public readonly int Fur1;

		public readonly int Fur2;

		private BedParts(List<Part> parts, int fur1, int fur2)
		{
			Parts = parts;
			Fur1 = fur1;
			Fur2 = fur2;
		}

		public static BedParts Read(Vector3[] vertices, int[] triangles, out string why)
		{
			List<Part> list = Split(vertices, triangles);
			int fur = -1;
			int fur2 = -1;
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (IsFur(list[i]))
				{
					if (list[i].Centre.x < 0f)
					{
						fur = i;
						num++;
					}
					else
					{
						fur2 = i;
						num2++;
					}
				}
			}
			if (num != 1 || num2 != 1)
			{
				why = $"{list.Count} pieces, {num} fur(s) on side 1 and {num2} on side 2 (one each expected)";
				return null;
			}
			why = null;
			return new BedParts(list, fur, fur2);
		}

		private static bool IsFur(Part p)
		{
			Vector3 size = p.Size;
			float num = Mathf.Abs(p.Centre.x);
			if (p.Min.y < 0.15f && p.Max.y > 0.35f && p.Max.y < 0.75f && num > 0.5f && num < 1f && size.x > 0.6f && size.x < 1.3f)
			{
				return size.z > 0.8f;
			}
			return false;
		}

		public bool IsGround(int part)
		{
			if (part != Fur1 && part != Fur2)
			{
				return Parts[part].Max.y < 0.6f;
			}
			return false;
		}

		public int[] TrianglesWithout(int[] triangles, bool fur1, bool fur2)
		{
			HashSet<int> hashSet = new HashSet<int>();
			if (fur1)
			{
				hashSet.UnionWith(Parts[Fur1].Triangles);
			}
			if (fur2)
			{
				hashSet.UnionWith(Parts[Fur2].Triangles);
			}
			List<int> list = new List<int>(triangles.Length);
			for (int i = 0; i < triangles.Length / 3; i++)
			{
				if (!hashSet.Contains(i))
				{
					list.Add(triangles[i * 3]);
					list.Add(triangles[i * 3 + 1]);
					list.Add(triangles[i * 3 + 2]);
				}
			}
			return list.ToArray();
		}

		public void Ground(Vector3[] vertices, int[] triangles, Field into)
		{
			for (int i = 0; i < Parts.Count; i++)
			{
				if (!IsGround(i))
				{
					continue;
				}
				foreach (int triangle in Parts[i].Triangles)
				{
					into.Triangle(vertices[triangles[triangle * 3]], vertices[triangles[triangle * 3 + 1]], vertices[triangles[triangle * 3 + 2]]);
				}
			}
		}

		private static List<Part> Split(Vector3[] v, int[] tris)
		{
			int[] parent = new int[v.Length];
			for (int i = 0; i < parent.Length; i++)
			{
				parent[i] = i;
			}
			Dictionary<(int, int, int), int> dictionary = new Dictionary<(int, int, int), int>();
			for (int j = 0; j < v.Length; j++)
			{
				(int, int, int) key = (Mathf.RoundToInt(v[j].x * 10000f), Mathf.RoundToInt(v[j].y * 10000f), Mathf.RoundToInt(v[j].z * 10000f));
				if (dictionary.TryGetValue(key, out var value))
				{
					Join(j, value);
				}
				else
				{
					dictionary[key] = j;
				}
			}
			for (int k = 0; k + 2 < tris.Length; k += 3)
			{
				Join(tris[k], tris[k + 1]);
				Join(tris[k + 1], tris[k + 2]);
			}
			Dictionary<int, Part> dictionary2 = new Dictionary<int, Part>();
			List<Part> list = new List<Part>();
			for (int l = 0; l < tris.Length / 3; l++)
			{
				int key2 = Find(tris[l * 3]);
				if (!dictionary2.TryGetValue(key2, out var value2))
				{
					value2 = (dictionary2[key2] = new Part());
					list.Add(value2);
				}
				value2.Triangles.Add(l);
				for (int m = 0; m < 3; m++)
				{
					Vector3 rhs = v[tris[l * 3 + m]];
					value2.Min = Vector3.Min(value2.Min, rhs);
					value2.Max = Vector3.Max(value2.Max, rhs);
				}
			}
			return list;
			int Find(int a)
			{
				while (parent[a] != a)
				{
					a = (parent[a] = parent[parent[a]]);
				}
				return a;
			}
			void Join(int a, int b)
			{
				parent[Find(a)] = Find(b);
			}
		}
	}
	internal sealed class Sheet
	{
		public Vector3[] Rest;

		public Vector2[] Uv;

		public Vector2[] Flat;

		public int[] Triangles;

		public float MinX;

		public float MaxX;

		public float MinZ;

		public float MaxZ;

		public float FitError;

		public int FacingDown;

		public static Sheet From(Vector3[] vertices, Vector2[] uvs, int[] triangles, Part fur, int finer)
		{
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			List<Vector3> list = new List<Vector3>();
			List<Vector2> list2 = new List<Vector2>();
			List<int> list3 = new List<int>();
			foreach (int triangle in fur.Triangles)
			{
				for (int i = 0; i < 3; i++)
				{
					int num = triangles[triangle * 3 + i];
					if (!dictionary.TryGetValue(num, out var value))
					{
						value = (dictionary[num] = list.Count);
						list.Add(vertices[num]);
						list2.Add(uvs[num]);
					}
					list3.Add(value);
				}
			}
			Vector2 scale = UvScale(list, list2, list3);
			for (int j = 0; j < finer; j++)
			{
				Subdivide(list, list2, list3);
			}
			Sheet sheet = new Sheet();
			sheet.Rest = list.ToArray();
			sheet.Uv = list2.ToArray();
			sheet.Triangles = list3.ToArray();
			sheet.Flatten(scale);
			return sheet;
		}

		private static Vector2 UvScale(List<Vector3> pos, List<Vector2> uv, List<int> tri)
		{
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			double num5 = 0.0;
			double num6 = 0.0;
			double num7 = 0.0;
			for (int i = 0; i < tri.Count; i += 3)
			{
				for (int j = 0; j < 3; j++)
				{
					int index = tri[i + j];
					int index2 = tri[i + (j + 1) % 3];
					double num8 = uv[index2].x - uv[index].x;
					double num9 = uv[index2].y - uv[index].y;
					double num10 = (pos[index2] - pos[index]).sqrMagnitude;
					double num11 = num8 * num8;
					double num12 = num9 * num9;
					num += num11 * num11;
					num2 += num11 * num12;
					num3 += num12 * num12;
					num4 += num10 * num11;
					num5 += num10 * num12;
					num6 += Math.Sqrt(num11 + num12) * Math.Sqrt(num10);
					num7 += num11 + num12;
				}
			}
			double num13 = num * num3 - num2 * num2;
			if (Math.Abs(num13) > 1E-18)
			{
				double num14 = (num4 * num3 - num2 * num5) / num13;
				double num15 = (num * num5 - num2 * num4) / num13;
				if (num14 > 0.0 && num15 > 0.0)
				{
					return new Vector2((float)Math.Sqrt(num14), (float)Math.Sqrt(num15));
				}
			}
			float num16 = ((num7 > 0.0) ? ((float)(num6 / num7)) : 1f);
			return new Vector2(num16, num16);
		}

		private static void Subdivide(List<Vector3> pos, List<Vector2> uv, List<int> tri)
		{
			Dictionary<(int, int), int> mid = new Dictionary<(int, int), int>();
			List<int> list = new List<int>(tri.Count * 4);
			for (int k = 0; k < tri.Count; k += 3)
			{
				int num = tri[k];
				int num2 = tri[k + 1];
				int num3 = tri[k + 2];
				int num4 = Mid(num, num2);
				int num5 = Mid(num2, num3);
				int num6 = Mid(num3, num);
				list.AddRange(new int[12]
				{
					num, num4, num6, num4, num2, num5, num6, num5, num3, num4,
					num5, num6
				});
			}
			tri.Clear();
			tri.AddRange(list);
			int Mid(int i, int j)
			{
				(int, int) key = ((i < j) ? (i, j) : (j, i));
				if (mid.TryGetValue(key, out var value))
				{
					return value;
				}
				value = (mid[key] = pos.Count);
				pos.Add((pos[i] + pos[j]) * 0.5f);
				uv.Add((uv[i] + uv[j]) * 0.5f);
				return value;
			}
		}

		private void Flatten(Vector2 scale)
		{
			int num = Rest.Length;
			Vector2[] array = new Vector2[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = new Vector2(Uv[i].x * scale.x, Uv[i].y * scale.y);
			}
			List<int> list = new List<int>();
			for (int j = 0; j < num; j++)
			{
				if (Rest[j].y > 0.35f)
				{
					list.Add(j);
				}
			}
			if (list.Count < 3)
			{
				for (int k = 0; k < num; k++)
				{
					list.Add(k);
				}
			}
			Fit(array, list, mirror: false, out var angle, out var move, out var error);
			Fit(array, list, mirror: true, out var angle2, out var move2, out var error2);
			bool flag = error2 < error;
			float angle3 = (flag ? angle2 : angle);
			Vector2 vector = (flag ? move2 : move);
			FitError = (flag ? error2 : error);
			Flat = new Vector2[num];
			MinX = (MinZ = float.MaxValue);
			MaxX = (MaxZ = float.MinValue);
			for (int l = 0; l < num; l++)
			{
				Vector2 vector2 = Turn(Mirror(array[l], flag), angle3) + vector;
				Flat[l] = vector2;
				MinX = Mathf.Min(MinX, vector2.x);
				MaxX = Mathf.Max(MaxX, vector2.x);
				MinZ = Mathf.Min(MinZ, vector2.y);
				MaxZ = Mathf.Max(MaxZ, vector2.y);
			}
			int num2 = 0;
			for (int m = 0; m < Triangles.Length; m += 3)
			{
				if (FlatUp(m) < 0f)
				{
					num2++;
				}
			}
			if (num2 * 2 > Triangles.Length / 3)
			{
				for (int n = 0; n < Triangles.Length; n += 3)
				{
					ref int reference = ref Triangles[n + 1];
					ref int reference2 = ref Triangles[n + 2];
					int num3 = Triangles[n + 2];
					int num4 = Triangles[n + 1];
					reference = num3;
					reference2 = num4;
				}
			}
			FacingDown = 0;
			for (int num5 = 0; num5 < Triangles.Length; num5 += 3)
			{
				if (FlatUp(num5) < 0f)
				{
					FacingDown++;
				}
			}
		}

		private float FlatUp(int t)
		{
			Vector2 vector = Flat[Triangles[t]];
			Vector2 vector2 = Flat[Triangles[t + 1]];
			Vector2 vector3 = Flat[Triangles[t + 2]];
			return Vector3.Cross(new Vector3(vector2.x - vector.x, 0f, vector2.y - vector.y), new Vector3(vector3.x - vector.x, 0f, vector3.y - vector.y)).y;
		}

		private static Vector2 Mirror(Vector2 p, bool mirror)
		{
			if (!mirror)
			{
				return p;
			}
			return new Vector2(0f - p.x, p.y);
		}

		private static Vector2 Turn(Vector2 p, float angle)
		{
			float num = Mathf.Cos(angle);
			float num2 = Mathf.Sin(angle);
			return new Vector2(num * p.x - num2 * p.y, num2 * p.x + num * p.y);
		}

		private void Fit(Vector2[] flat, List<int> use, bool mirror, out float angle, out Vector2 move, out float error)
		{
			Vector2 zero = Vector2.zero;
			Vector2 zero2 = Vector2.zero;
			foreach (int item in use)
			{
				zero += Mirror(flat[item], mirror);
				zero2 += new Vector2(Rest[item].x, Rest[item].z);
			}
			zero /= (float)use.Count;
			zero2 /= (float)use.Count;
			double num = 0.0;
			double num2 = 0.0;
			foreach (int item2 in use)
			{
				Vector2 vector = Mirror(flat[item2], mirror) - zero;
				Vector2 vector2 = new Vector2(Rest[item2].x, Rest[item2].z) - zero2;
				num += (double)(vector.x * vector2.y - vector.y * vector2.x);
				num2 += (double)(vector.x * vector2.x + vector.y * vector2.y);
			}
			angle = (float)Math.Atan2(num, num2);
			move = zero2 - Turn(zero, angle);
			double num3 = 0.0;
			foreach (int item3 in use)
			{
				num3 += (double)(Turn(Mirror(flat[item3], mirror), angle) + move - new Vector2(Rest[item3].x, Rest[item3].z)).sqrMagnitude;
			}
			error = (float)Math.Sqrt(num3 / (double)use.Count);
		}
	}
	internal sealed class Field
	{
		public readonly float X0;

		public readonly float Z0;

		public readonly float Cell;

		public readonly int Nx;

		public readonly int Nz;

		public readonly float[] H;

		public Field(float x0, float z0, float x1, float z1, float cell)
		{
			X0 = x0;
			Z0 = z0;
			Cell = cell;
			Nx = Mathf.CeilToInt((x1 - x0) / cell);
			Nz = Mathf.CeilToInt((z1 - z0) / cell);
			H = new float[Nx * Nz];
		}

		public Field(Field like)
			: this(like.X0, like.Z0, like.X0 + (float)like.Nx * like.Cell, like.Z0 + (float)like.Nz * like.Cell, like.Cell)
		{
		}

		public float X(int i)
		{
			return X0 + ((float)i + 0.5f) * Cell;
		}

		public float Z(int j)
		{
			return Z0 + ((float)j + 0.5f) * Cell;
		}

		public void Fill(float value)
		{
			for (int i = 0; i < H.Length; i++)
			{
				H[i] = value;
			}
		}

		public void CopyFrom(Field other)
		{
			Array.Copy(other.H, H, H.Length);
		}

		public void Triangle(Vector3 a, Vector3 b, Vector3 c)
		{
			float num = (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
			if (Mathf.Abs(num) < 1E-09f)
			{
				return;
			}
			int num2 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - X0) / Cell));
			int num3 = Mathf.Min(Nx - 1, Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - X0) / Cell));
			int num4 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - Z0) / Cell));
			int num5 = Mathf.Min(Nz - 1, Mathf.FloorToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - Z0) / Cell));
			for (int i = num4; i <= num5; i++)
			{
				float num6 = Z(i);
				for (int j = num2; j <= num3; j++)
				{
					float num7 = X(j);
					float num8 = ((num7 - a.x) * (c.z - a.z) - (c.x - a.x) * (num6 - a.z)) / num;
					float num9 = ((b.x - a.x) * (num6 - a.z) - (num7 - a.x) * (b.z - a.z)) / num;
					float num10 = 1f - num8 - num9;
					if (!(num10 < -0.0001f) && !(num8 < -0.0001f) && !(num9 < -0.0001f))
					{
						Raise(j, i, num10 * a.y + num8 * b.y + num9 * c.y);
					}
				}
			}
		}

		public void Point(Vector3 p, int cells)
		{
			int num = Mathf.FloorToInt((p.x - X0) / Cell);
			int num2 = Mathf.FloorToInt((p.z - Z0) / Cell);
			for (int i = Mathf.Max(0, num2 - cells); i <= Mathf.Min(Nz - 1, num2 + cells); i++)
			{
				for (int j = Mathf.Max(0, num - cells); j <= Mathf.Min(Nx - 1, num + cells); j++)
				{
					Raise(j, i, p.y);
				}
			}
		}

		public void Sphere(Vector3 c, float radius)
		{
			int num = Mathf.Max(0, Mathf.FloorToInt((c.x - radius - X0) / Cell));
			int num2 = Mathf.Min(Nx - 1, Mathf.FloorToInt((c.x + radius - X0) / Cell));
			int num3 = Mathf.Max(0, Mathf.FloorToInt((c.z - radius - Z0) / Cell));
			int num4 = Mathf.Min(Nz - 1, Mathf.FloorToInt((c.z + radius - Z0) / Cell));
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					float num5 = X(j) - c.x;
					float num6 = Z(i) - c.z;
					float num7 = radius * radius - num5 * num5 - num6 * num6;
					if (num7 >= 0f)
					{
						Raise(j, i, c.y + Mathf.Sqrt(num7));
					}
				}
			}
		}

		public void Capsule(Vector3 a, Vector3 b, float radius)
		{
			int num = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / Cell));
			for (int i = 0; i <= num; i++)
			{
				Sphere(Vector3.Lerp(a, b, (float)i / (float)num), radius);
			}
		}

		public void Box(Vector3 min, Vector3 max)
		{
			int num = Mathf.Max(0, Mathf.FloorToInt((min.x - X0) / Cell));
			int num2 = Mathf.Min(Nx - 1, Mathf.FloorToInt((max.x - X0) / Cell));
			int num3 = Mathf.Max(0, Mathf.FloorToInt((min.z - Z0) / Cell));
			int num4 = Mathf.Min(Nz - 1, Mathf.FloorToInt((max.z - Z0) / Cell));
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					Raise(j, i, max.y);
				}
			}
		}

		private void Raise(int i, int j, float y)
		{
			int num = j * Nx + i;
			if (y > H[num])
			{
				H[num] = y;
			}
		}

		public void Close(int cells, float[] scratch)
		{
			Window(H, scratch, cells, max: true, alongX: true);
			Window(scratch, H, cells, max: true, alongX: false);
			Window(H, scratch, cells, max: false, alongX: true);
			Window(scratch, H, cells, max: false, alongX: false);
		}

		public void Slope(float rise)
		{
			float num = rise * Cell;
			for (int i = 0; i < Nz; i++)
			{
				for (int j = 1; j < Nx; j++)
				{
					H[i * Nx + j] = Mathf.Max(H[i * Nx + j], H[i * Nx + j - 1] - num);
				}
				for (int num2 = Nx - 2; num2 >= 0; num2--)
				{
					H[i * Nx + num2] = Mathf.Max(H[i * Nx + num2], H[i * Nx + num2 + 1] - num);
				}
			}
			for (int k = 0; k < Nx; k++)
			{
				for (int l = 1; l < Nz; l++)
				{
					H[l * Nx + k] = Mathf.Max(H[l * Nx + k], H[(l - 1) * Nx + k] - num);
				}
				for (int num3 = Nz - 2; num3 >= 0; num3--)
				{
					H[num3 * Nx + k] = Mathf.Max(H[num3 * Nx + k], H[(num3 + 1) * Nx + k] - num);
				}
			}
		}

		public void Dilate(int cells, float[] scratch)
		{
			Window(H, scratch, cells, max: true, alongX: true);
			Window(scratch, H, cells, max: true, alongX: false);
		}

		public void Blur(int cells, int passes, float[] scratch)
		{
			for (int i = 0; i < passes; i++)
			{
				Mean(H, scratch, cells, alongX: true);
				Mean(scratch, H, cells, alongX: false);
			}
		}

		private void Window(float[] from, float[] to, int r, bool max, bool alongX)
		{
			int num = (alongX ? Nx : Nz);
			int num2 = (alongX ? Nz : Nx);
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					float num3 = (max ? float.MinValue : float.MaxValue);
					for (int k = Mathf.Max(0, j - r); k <= Mathf.Min(num - 1, j + r); k++)
					{
						float b = from[alongX ? (i * Nx + k) : (k * Nx + i)];
						num3 = (max ? Mathf.Max(num3, b) : Mathf.Min(num3, b));
					}
					to[alongX ? (i * Nx + j) : (j * Nx + i)] = num3;
				}
			}
		}

		private void Mean(float[] from, float[] to, int r, bool alongX)
		{
			int num = (alongX ? Nx : Nz);
			int num2 = (alongX ? Nz : Nx);
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					float num3 = 0f;
					int num4 = 0;
					for (int k = Mathf.Max(0, j - r); k <= Mathf.Min(num - 1, j + r); k++)
					{
						num3 += from[alongX ? (i * Nx + k) : (k * Nx + i)];
						num4++;
					}
					to[alongX ? (i * Nx + j) : (j * Nx + i)] = num3 / (float)num4;
				}
			}
		}

		public float Sample(float x, float z)
		{
			float num = Mathf.Clamp((x - X0) / Cell - 0.5f, 0f, (float)Nx - 1.001f);
			float num2 = Mathf.Clamp((z - Z0) / Cell - 0.5f, 0f, (float)Nz - 1.001f);
			int num3 = (int)num;
			int num4 = (int)num2;
			float t = num - (float)num3;
			float t2 = num2 - (float)num4;
			float a = Mathf.Lerp(H[num4 * Nx + num3], H[num4 * Nx + num3 + 1], t);
			float b = Mathf.Lerp(H[(num4 + 1) * Nx + num3], H[(num4 + 1) * Nx + num3 + 1], t);
			return Mathf.Lerp(a, b, t2);
		}
	}
	internal struct Cover
	{
		public float CentreX;

		public float TopZ;

		public float FootZ;

		public int TuckSide;

		public float TuckX;

		public float Lift;
	}
	internal static class Drape
	{
		private sealed class Row
		{
			private readonly float[] _upLength;

			private readonly float[] _upHeight;

			private readonly float[] _downLength;

			private readonly float[] _downHeight;

			private readonly float _centre;

			private readonly float _cell;

			public Row(Field f, int j, float centre)
			{
				_centre = centre;
				_cell = f.Cell;
				float z = f.Z(j);
				Measure(f, z, 1, f.X0 + (float)f.Nx * f.Cell, out _upLength, out _upHeight);
				Measure(f, z, -1, f.X0, out _downLength, out _downHeight);
			}

			private void Measure(Field f, float z, int dir, float end, out float[] length, out float[] height)
			{
				int num = Mathf.Max(2, Mathf.FloorToInt(Mathf.Abs(end - _centre) / _cell));
				length = new float[num];
				height = new float[num];
				for (int i = 0; i < num; i++)
				{
					height[i] = f.Sample(_centre + (float)(dir * i) * _cell, z);
					length[i] = ((i == 0) ? 0f : (length[i - 1] + Mathf.Sqrt(_cell * _cell + (height[i] - height[i - 1]) * (height[i] - height[i - 1]))));
				}
			}

			public Vector2 At(float along)
			{
				int num = ((along >= 0f) ? 1 : (-1));
				float[] array = ((num > 0) ? _upLength : _downLength);
				float[] array2 = ((num > 0) ? _upHeight : _downHeight);
				float num2 = Mathf.Abs(along);
				int num3 = array.Length;
				if (num2 >= array[num3 - 1])
				{
					float num4 = num2 - array[num3 - 1];
					float num5 = Mathf.Min(num4, Mathf.Max(0f, array2[num3 - 1] - 0.02f));
					return new Vector2(_centre + (float)num * ((float)(num3 - 1) * _cell + num4 - num5), array2[num3 - 1] - num5);
				}
				int num6 = 0;
				int num7 = num3 - 1;
				while (num7 - num6 > 1)
				{
					int num8 = (num6 + num7) / 2;
					if (array[num8] <= num2)
					{
						num6 = num8;
					}
					else
					{
						num7 = num8;
					}
				}
				float num9 = (num2 - array[num6]) / Mathf.Max(1E-06f, array[num7] - array[num6]);
				return new Vector2(_centre + (float)num * ((float)num6 + num9) * _cell, Mathf.Lerp(array2[num6], array2[num7], num9));
			}
		}

		public const float Cell = 0.02f;

		public const float Gap = 0.035f;

		public const int CloseCells = 8;

		public const int BlurCells = 2;

		public const int BlurPasses = 2;

		public const float MinStretch = 0.85f;

		public const float MaxStretch = 1.3f;

		public const float Across = 1.2f;

		public const float Flank = 1.2f;

		public const float Floor = 0.02f;

		public const float RollCore = 0.025f;

		public const float RollLayer = 0.009f;

		public static Field NewField()
		{
			return new Field(-1.5f, -1.3f, 1.5f, 1.9f, 0.02f);
		}

		public static void Shape(Field ground, Field body, Field into, float[] scratch)
		{
			float[] h = ground.H;
			float[] h2 = body.H;
			float[] h3 = into.H;
			for (int i = 0; i < h3.Length; i++)
			{
				h3[i] = Mathf.Max(h[i], h2[i] + 0.035f);
			}
			into.Close(8, scratch);
			into.Slope(1.2f);
			into.Dilate(4, scratch);
			into.Blur(2, 2, scratch);
			for (int j = 0; j < h3.Length; j++)
			{
				h3[j] = Mathf.Max(h3[j], h[j] + 0.004f);
			}
		}

		public static void Tuck(Field f, in Cover c)
		{
			if (c.TuckSide == 0)
			{
				return;
			}
			for (int i = 0; i < f.Nx; i++)
			{
				float num = (f.X(i) - c.TuckX) * (float)c.TuckSide;
				if (!(num <= 0f))
				{
					for (int j = 0; j < f.Nz; j++)
					{
						f.H[j * f.Nx + i] -= num * 3f;
					}
				}
			}
		}

		public static float Lay(Sheet sheet, Field f, in Cover c, Vector3[] into)
		{
			float num = sheet.MaxZ - sheet.MinZ;
			float num2 = Mathf.Clamp((c.FootZ - c.TopZ) / num, 0.85f, 1.3f);
			float num3 = (sheet.MinX + sheet.MaxX) * 0.5f;
			float centre = c.CentreX;
			Dictionary<int, Row> rows = new Dictionary<int, Row>();
			for (int i = 0; i < sheet.Flat.Length; i++)
			{
				Vector2 vector = sheet.Flat[i];
				float num4 = c.TopZ + (vector.y - sheet.MinZ) * num2;
				float along = (vector.x - num3) * 1.2f;
				float num5 = (num4 - f.Z0) / f.Cell - 0.5f;
				int num6 = Mathf.FloorToInt(num5);
				float t = Mathf.Clamp01(num5 - (float)num6);
				Vector2 a = RowAt(num6).At(along);
				Vector2 b = RowAt(num6 + 1).At(along);
				Vector2 vector2 = Vector2.Lerp(a, b, t);
				float num7 = ((vector2.x > f.X0 && vector2.x < f.X0 + (float)f.Nx * f.Cell) ? Mathf.Max(vector2.y, f.Sample(vector2.x, num4)) : vector2.y);
				into[i] = new Vector3(vector2.x, num7 + c.Lift, num4);
			}
			return num2;
			Row RowAt(int j)
			{
				j = Mathf.Clamp(j, 0, f.Nz - 1);
				if (!rows.TryGetValue(j, out var value))
				{
					value = (rows[j] = new Row(f, j, centre));
				}
				return value;
			}
		}

		public static void Roll(Sheet sheet, Field f, in Cover c, float stretch, Vector3[] laid, float front, Vector3[] into)
		{
			float num = (sheet.MaxZ - sheet.MinZ) * stretch;
			front = Mathf.Clamp(front, 0f, num);
			float num2 = SpiralAngle(num - front);
			float num3 = 0.025f + 0.009f * num2 / (MathF.PI * 2f);
			float num4 = c.TopZ + num - front;
			for (int i = 0; i < sheet.Flat.Length; i++)
			{
				float num5 = (sheet.MaxZ - sheet.Flat[i].y) * stretch;
				if (num5 <= front)
				{
					into[i] = laid[i];
					continue;
				}
				float num6 = SpiralAngle(num - num5);
				float num7 = 0.025f + 0.009f * num6 / (MathF.PI * 2f);
				float f2 = num2 - num6;
				float x = laid[i].x;
				float num8 = float.MinValue;
				for (int j = -2; j <= 2; j++)
				{
					num8 = Mathf.Max(num8, f.Sample(x, num4 + (float)j * 0.5f * num3));
				}
				num8 += c.Lift;
				into[i] = new Vector3(x, num8 + num3 - num7 * Mathf.Cos(f2), num4 - num7 * Mathf.Sin(f2));
			}
		}

		private static float SpiralAngle(float wound)
		{
			float num = 0.0014323944f;
			return (-0.025f + Mathf.Sqrt(0.00062500004f + 2f * num * Mathf.Max(0f, wound))) / num;
		}
	}
	internal static class Blankets
	{
		internal sealed class Model
		{
			public Mesh Original;

			public BedParts Parts;

			public int[] Triangles;

			public readonly Mesh[] Without = new Mesh[4];

			public Sheet[] Sheets;

			public Field Ground;
		}

		public const string ObjectName = "DoubleBed_blanket";

		public const float SeenWithin = 40f;

		public const float FootZ = 1.4f;

		public const float Outward = 0.08f;

		private const int Finer = 3;

		private static readonly Dictionary<Mesh, Model> s_models = new Dictionary<Mesh, Model>();

		private static readonly Dictionary<Mesh, Mesh> s_originals = new Dictionary<Mesh, Mesh>();

		private static readonly HashSet<Mesh> s_unknown = new HashSet<Mesh>();

		public static bool Drawn => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

		public static void Wake(Bed bed)
		{
			if (Drawn && !BedSides.IsSecond(bed) && !(BedSides.SecondSide(bed.gameObject) == null) && !(Access.BedView(bed) == null) && Access.BedView(bed).GetZDO() != null && !bed.TryGetComponent<BedBlankets>(out var _))
			{
				bed.gameObject.AddComponent<BedBlankets>();
			}
		}

		public static void Install()
		{
			Bed[] array = UnityEngine.Object.FindObjectsByType<Bed>(FindObjectsSortMode.None);
			foreach (Bed bed in array)
			{
				if (bed != null)
				{
					Wake(bed);
				}
			}
		}

		public static void Uninstall()
		{
			BedBlankets[] array = UnityEngine.Object.FindObjectsByType<BedBlankets>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (BedBlankets obj in array)
			{
				obj.Restore();
				UnityEngine.Object.DestroyImmediate(obj);
			}
			foreach (Model value in s_models.Values)
			{
				Mesh[] without = value.Without;
				foreach (Mesh mesh in without)
				{
					if (mesh != null)
					{
						UnityEngine.Object.DestroyImmediate(mesh);
					}
				}
			}
			s_models.Clear();
			s_originals.Clear();
			s_unknown.Clear();
			Measure.Uninstall();
		}

		public static Mesh OriginalOf(Mesh mesh)
		{
			if (!(mesh != null) || !s_originals.TryGetValue(mesh, out var value))
			{
				return mesh;
			}
			return value;
		}

		public static Model ModelOf(Mesh original)
		{
			if (original == null || s_unknown.Contains(original))
			{
				return null;
			}
			if (s_models.TryGetValue(original, out var value))
			{
				return value;
			}
			string why;
			if (!original.isReadable)
			{
				why = "its mesh cannot be read";
			}
			else if (original.subMeshCount != 1)
			{
				why = $"{original.subMeshCount} submeshes";
			}
			else
			{
				int[] triangles = original.triangles;
				BedParts bedParts = BedParts.Read(original.vertices, triangles, out why);
				if (bedParts != null)
				{
					Dictionary<Mesh, Model> dictionary = s_models;
					Model obj = new Model
					{
						Original = original,
						Parts = bedParts,
						Triangles = triangles
					};
					value = obj;
					dictionary[original] = obj;
					return value;
				}
			}
			s_unknown.Add(original);
			Plugin.Log.LogWarning("No blankets on the dragon bed's mesh '" + original.name + "': " + why + ". The bed works as before.");
			return null;
		}

		public static Mesh Without(Model model, int mask)
		{
			if (mask == 0)
			{
				return model.Original;
			}
			Mesh mesh = model.Without[mask];
			if (mesh == null)
			{
				mesh = UnityEngine.Object.Instantiate(model.Original);
				mesh.name = string.Format("{0}_{1}{2}", model.Original.name, "DoubleBed_blanket", mask);
				mesh.SetTriangles(model.Parts.TrianglesWithout(model.Triangles, (mask & 1) != 0, (mask & 2) != 0), 0, calculateBounds: false);
				model.Without[mask] = mesh;
				s_originals[mesh] = model.Original;
			}
			return mesh;
		}

		public static bool Cloth(Model model)
		{
			if (model.Sheets != null)
			{
				return true;
			}
			Vector3[] vertices = model.Original.vertices;
			Vector2[] uv = model.Original.uv;
			if (uv == null || uv.Length != vertices.Length)
			{
				return false;
			}
			BedParts parts = model.Parts;
			model.Sheets = new Sheet[2]
			{
				Sheet.From(vertices, uv, model.Triangles, parts.Parts[parts.Fur1], 3),
				Sheet.From(vertices, uv, model.Triangles, parts.Parts[parts.Fur2], 3)
			};
			model.Ground = Drape.NewField();
			model.Ground.Fill(0f);
			parts.Ground(vertices, model.Triangles, model.Ground);
			return true;
		}
	}
	internal sealed class BedBlankets : MonoBehaviour
	{
		private const float LookEvery = 0.1f;

		private Bed m_first;

		private Bed m_second;

		private Transform m_middle;

		private WearNTear m_wear;

		private readonly List<(MeshFilter Filter, Blankets.Model Model)> m_filters = new List<(MeshFilter, Blankets.Model)>();

		private Blankets.Model m_near;

		private MeshRenderer m_newRenderer;

		private MeshRenderer m_wornRenderer;

		private Blanket[] m_blankets;

		private bool m_tried;

		private int m_mask;

		private float m_nextLook;

		public Blanket this[int side]
		{
			get
			{
				Blanket[] blankets = m_blankets;
				if (blankets == null)
				{
					return null;
				}
				return blankets[side];
			}
		}

		public Blankets.Model Near => m_near;

		private bool Ready()
		{
			if (m_blankets != null)
			{
				return true;
			}
			if (m_tried)
			{
				return false;
			}
			m_tried = true;
			m_first = GetComponent<Bed>();
			m_second = BedSides.SecondSide(base.gameObject);
			m_middle = base.transform.Find("DoubleBed_side2/spawnpoint_both");
			m_wear = GetComponent<WearNTear>();
			if (m_first == null || m_second == null || m_middle == null || m_wear == null || m_wear.m_new == null)
			{
				return false;
			}
			GameObject[] array = new GameObject[2] { m_wear.m_new, m_wear.m_worn };
			foreach (GameObject gameObject in array)
			{
				if (gameObject == null)
				{
					continue;
				}
				MeshFilter[] componentsInChildren = gameObject.GetComponentsInChildren<MeshFilter>(includeInactive: true);
				foreach (MeshFilter filter in componentsInChildren)
				{
					if (m_filters.Exists(((MeshFilter Filter, Blankets.Model Model) f) => f.Filter == filter))
					{
						continue;
					}
					Blankets.Model model = Blankets.ModelOf(Blankets.OriginalOf(filter.sharedMesh));
					if (model != null && InBedSpace(filter.transform))
					{
						m_filters.Add((filter, model));
						if (m_near == null || model.Original.vertexCount > m_near.Original.vertexCount)
						{
							m_near = model;
						}
					}
				}
			}
			if (m_near == null || !Blankets.Cloth(m_near))
			{
				return false;
			}
			foreach (var filter2 in m_filters)
			{
				var (meshFilter, _) = filter2;
				if (filter2.Model == m_near)
				{
					if (meshFilter.transform.IsChildOf(m_wear.m_new.transform))
					{
						m_newRenderer = meshFilter.GetComponent<MeshRenderer>();
					}
					else
					{
						m_wornRenderer = meshFilter.GetComponent<MeshRenderer>();
					}
				}
			}
			if (m_newRenderer == null)
			{
				return false;
			}
			m_blankets = new Blanket[2]
			{
				new Blanket(this, 0, m_near.Sheets[0]),
				new Blanket(this, 1, m_near.Sheets[1])
			};
			return true;
		}

		private bool InBedSpace(Transform t)
		{
			Matrix4x4 matrix4x = base.transform.worldToLocalMatrix * t.localToWorldMatrix;
			for (int i = 0; i < 16; i++)
			{
				if (Mathf.Abs(matrix4x[i] - Matrix4x4.identity[i]) > 0.001f)
				{
					return false;
				}
			}
			return true;
		}

		private void Update()
		{
			if (!Ready())
			{
				base.enabled = false;
				return;
			}
			if (Plugin.BedBlankets == null || !Plugin.BedBlankets.Value || !Seen() || Broken())
			{
				Rest();
				return;
			}
			if (Time.time >= m_nextLook)
			{
				m_nextLook = Time.time + 0.1f;
				Look();
			}
			MeshRenderer look = ((m_wornRenderer != null && m_wear.m_worn != m_wear.m_new && m_wear.m_worn.activeInHierarchy) ? m_wornRenderer : m_newRenderer);
			int num = 0;
			for (int i = 0; i < 2; i++)
			{
				m_blankets[i].Tick(look);
				if (m_blankets[i].Showing)
				{
					num |= 1 << i;
				}
			}
			Show(num);
		}

		private bool Seen()
		{
			Transform transform = ((GameCamera.instance != null) ? GameCamera.instance.transform : ((Camera.main != null) ? Camera.main.transform : null));
			if (transform != null)
			{
				return (transform.position - base.transform.position).sqrMagnitude < 1600f;
			}
			return false;
		}

		private bool Broken()
		{
			if (m_wear.m_broken != null && m_wear.m_broken != m_wear.m_new)
			{
				return m_wear.m_broken.activeInHierarchy;
			}
			return false;
		}

		private void Look()
		{
			Vector3 b = base.transform.InverseTransformPoint(m_first.m_spawnPoint.position);
			Vector3 b2 = base.transform.InverseTransformPoint(m_second.m_spawnPoint.position);
			Vector3 vector = base.transform.InverseTransformPoint(m_middle.position);
			Player player = null;
			Player player2 = null;
			Player player3 = null;
			foreach (Player allPlayer in Player.GetAllPlayers())
			{
				if (allPlayer == null || !allPlayer.InBed())
				{
					continue;
				}
				Vector3 a = base.transform.InverseTransformPoint(allPlayer.transform.position);
				if (!(Mathf.Abs(a.y - vector.y) > 0.6f))
				{
					if (Across(a, vector) < 0.25f)
					{
						player3 = allPlayer;
					}
					else if (Across(a, b) < 0.3f)
					{
						player = allPlayer;
					}
					else if (Across(a, b2) < 0.3f)
					{
						player2 = allPlayer;
					}
				}
			}
			Cover[] array = new Cover[2];
			Player[] array2 = new Player[2];
			bool[] array3 = new bool[2];
			if (player3 != null)
			{
				array[0] = new Cover
				{
					CentreX = vector.x - 0.15f,
					FootZ = 1.4f
				};
				array[1] = new Cover
				{
					CentreX = vector.x + 0.15f,
					FootZ = 1.4f,
					Lift = 0.015f
				};
				array2[0] = (array2[1] = player3);
				array3[0] = (array3[1] = true);
			}
			else
			{
				array[0] = new Cover
				{
					CentreX = b.x - 0.08f,
					FootZ = 1.4f,
					TuckSide = 1,
					TuckX = vector.x - 0.06f
				};
				array[1] = new Cover
				{
					CentreX = b2.x + 0.08f,
					FootZ = 1.4f,
					TuckSide = -1,
					TuckX = vector.x + 0.06f
				};
				array2[0] = player;
				array2[1] = player2;
				array3[0] = player != null;
				array3[1] = player2 != null;
			}
			for (int i = 0; i < 2; i++)
			{
				m_blankets[i].Want(array3[i], array2[i], array[i], vector);
			}
		}

		private static float Across(Vector3 a, Vector3 b)
		{
			return new Vector2(a.x - b.x, a.z - b.z).magnitude;
		}

		private void Show(int mask)
		{
			if (mask == m_mask)
			{
				return;
			}
			m_mask = mask;
			foreach (var (meshFilter, model) in m_filters)
			{
				if (meshFilter != null)
				{
					meshFilter.sharedMesh = Blankets.Without(model, mask);
				}
			}
		}

		private void Rest()
		{
			if (m_blankets != null)
			{
				Blanket[] blankets = m_blankets;
				for (int i = 0; i < blankets.Length; i++)
				{
					blankets[i].Rest();
				}
			}
			Show(0);
		}

		public void Restore()
		{
			if (m_blankets != null)
			{
				Blanket[] blankets = m_blankets;
				for (int i = 0; i < blankets.Length; i++)
				{
					blankets[i].Destroy(now: true);
				}
			}
			m_blankets = null;
			m_mask = -1;
			Show(0);
		}

		private void OnDestroy()
		{
			if (m_blankets != null)
			{
				Blanket[] blankets = m_blankets;
				for (int i = 0; i < blankets.Length; i++)
				{
					blankets[i].Destroy(now: false);
				}
			}
		}
	}
	internal sealed class Blanket
	{
		private const float Gather = 0.25f;

		private const float GatherTime = 0.35f;

		private const float UnrollTime = 1.1f;

		private const float RollUpTime = 0.7f;

		private const float ReturnTime = 0.3f;

		private const float RollStart = 0.45f;

		private const float ReshapeTime = 1f;

		private const float LiftHeight = 0.2f;

		private const float RescanFirst = 2f;

		private const float Rescan = 6f;

		private const float RescanFirstFor = 8f;

		private const float SettleWait = 3f;

		private const float Underside = 0.004f;

		private const float ReshapeMean = 0.015f;

		private const float ReshapeFarthest = 0.1f;

		private readonly BedBlankets m_bed;

		private readonly int m_side;

		private readonly Sheet m_sheet;

		private readonly GameObject m_object;

		private readonly Mesh m_mesh;

		private readonly MeshRenderer m_renderer;

		private readonly Vector3[] m_from;

		private readonly Vector3[] m_to;

		private readonly Vector3[] m_now;

		private readonly Vector3[] m_laid;

		private readonly Vector3[] m_rolled;

		private readonly Vector3[] m_vertices;

		private readonly Vector3[] m_normals;

		private readonly Field m_body;

		private readonly Field m_shape;

		private readonly Field m_rollShape;

		private readonly float[] m_scratch;

		private float m_progress;

		private int m_direction;

		private bool m_reshaping;

		private float m_t;

		private Cover m_rollCover;

		private float m_rollStretch;

		private bool m_wanted;

		private Player m_player;

		private Cover m_cover;

		private Vector3 m_middle;

		private Measure m_measure;

		private float m_since;

		private float m_nextScan;

		private float m_nextSettle;

		private float m_lastHead = float.MaxValue;

		private Vector3 m_lastBody;

		private int m_still;

		private const float Slow = 1f;

		public string Report = "not measured yet";

		public float Stretch;

		public float TopZ;

		public string Reshaped = "";

		public bool Showing
		{
			get
			{
				if (!(m_progress > 0f))
				{
					return m_direction > 0;
				}
				return true;
			}
		}

		private bool IsOut
		{
			get
			{
				if (m_progress >= 1f)
				{
					return m_direction == 0;
				}
				return false;
			}
		}

		public string State
		{
			get
			{
				if (m_direction <= 0)
				{
					if (m_direction >= 0)
					{
						if (Showing)
						{
							if (!m_reshaping)
							{
								return "out";
							}
							return "reshaping";
						}
						return "in (the bed's fur)";
					}
					return $"going back ({m_progress:P0})";
				}
				return $"coming out ({m_progress:P0})";
			}
		}

		public Player Sleeper => m_player;

		public Field Shape => m_shape;

		public Field Body => m_body;

		public Vector3[] Now => m_now;

		public Sheet Sheet => m_sheet;

		public Blanket(BedBlankets bed, int side, Sheet sheet)
		{
			m_bed = bed;
			m_side = side;
			m_sheet = sheet;
			int num = sheet.Rest.Length;
			m_from = new Vector3[num];
			m_to = new Vector3[num];
			m_laid = new Vector3[num];
			m_rolled = new Vector3[num];
			m_now = (Vector3[])sheet.Rest.Clone();
			m_vertices = new Vector3[num * 2];
			m_normals = new Vector3[num * 2];
			m_body = new Field(bed.Near.Ground);
			m_shape = new Field(bed.Near.Ground);
			m_rollShape = new Field(bed.Near.Ground);
			m_scratch = new float[m_shape.H.Length];
			Vector2[] array = new Vector2[num * 2];
			sheet.Uv.CopyTo(array, 0);
			sheet.Uv.CopyTo(array, num);
			int[] triangles = sheet.Triangles;
			int[] array2 = new int[triangles.Length * 2];
			triangles.CopyTo(array2, 0);
			for (int i = 0; i < triangles.Length; i += 3)
			{
				array2[triangles.Length + i] = triangles[i] + num;
				array2[triangles.Length + i + 1] = triangles[i + 2] + num;
				array2[triangles.Length + i + 2] = triangles[i + 1] + num;
			}
			m_mesh = new Mesh
			{
				name = "DoubleBed_blanket" + (side + 1)
			};
			m_mesh.MarkDynamic();
			Write(first: true, array, array2);
			m_object = new GameObject("DoubleBed_blanket" + (side + 1));
			m_object.SetActive(value: false);
			m_object.layer = bed.gameObject.layer;
			m_object.transform.SetParent(bed.transform, worldPositionStays: false);
			m_object.AddComponent<MeshFilter>().sharedMesh = m_mesh;
			m_renderer = m_object.AddComponent<MeshRenderer>();
		}

		public void Want(bool wanted, Player player, Cover cover, Vector3 middle)
		{
			if (!wanted)
			{
				if (m_wanted)
				{
					m_wanted = false;
					m_player = null;
					m_measure?.Cancel();
					m_measure = null;
					GoBack();
				}
				return;
			}
			bool num = !m_wanted || player != m_player;
			bool flag = Mathf.Abs(cover.CentreX - m_cover.CentreX) > 0.01f || cover.TuckSide != m_cover.TuckSide;
			m_wanted = true;
			m_player = player;
			m_cover = cover;
			m_middle = middle;
			if (num)
			{
				m_measure?.Cancel();
				m_measure = null;
				m_since = Time.time;
				m_still = 0;
				m_lastHead = float.MaxValue;
				m_nextScan = 0f;
			}
			else if (flag)
			{
				m_nextScan = 0f;
			}
		}

		public void Tick(MeshRenderer look)
		{
			if (m_wanted)
			{
				if (m_measure == null && Time.time >= m_nextScan && Settled())
				{
					m_measure = Measure.Start(m_player, m_bed.transform, m_body, m_middle);
				}
				if (m_measure != null && m_measure.Poll())
				{
					Lay(m_measure);
					m_measure = null;
					m_nextScan = Time.time + ((Time.time - m_since < 11f) ? 2f : 6f);
				}
			}
			Animate(look);
		}

		private bool Settled()
		{
			if (m_player == null || Time.time - m_since > 3f)
			{
				return true;
			}
			if (Time.time < m_nextSettle)
			{
				return false;
			}
			m_nextSettle = Time.time + 0.25f;
			Transform transform = Measure.Bone(m_player, "Head");
			if (transform == null)
			{
				return false;
			}
			Vector3 vector = m_bed.transform.InverseTransformPoint(transform.position);
			Vector3 vector2 = m_bed.transform.InverseTransformPoint(m_player.transform.position);
			bool flag = vector.y < m_middle.y + 0.5f;
			bool flag2 = Mathf.Abs(vector.y - m_lastHead) < 0.01f && (vector2 - m_lastBody).sqrMagnitude < 0.0001f;
			m_lastHead = vector.y;
			m_lastBody = vector2;
			m_still = ((flag && flag2) ? (m_still + 1) : 0);
			return m_still >= 2;
		}

		private void Lay(Measure measure)
		{
			Field ground = m_bed.Near.Ground;
			Cover c = m_cover;
			c.TopZ = ((IsOut && Mathf.Abs(measure.TopZ - TopZ) < 0.04f) ? TopZ : measure.TopZ);
			Drape.Shape(ground, m_body, m_shape, m_scratch);
			Drape.Tuck(m_shape, in c);
			Stretch = Drape.Lay(m_sheet, m_shape, in c, m_laid);
			TopZ = c.TopZ;
			Report = measure.Report;
			if (!IsOut)
			{
				Keep(c);
				m_direction = 1;
				return;
			}
			Moved(m_laid, m_now, out var mean, out var farthest);
			if (m_reshaping || !(mean < 0.015f) || !(farthest < 0.1f))
			{
				Reshaped = $"last reshaped at {Time.time:F0} s ({mean * 100f:F1} cm on average, {farthest * 100f:F0} at most)";
				m_now.CopyTo(m_from, 0);
				Keep(c);
				m_reshaping = true;
				m_t = 0f;
			}
		}

		private void Keep(Cover cover)
		{
			m_laid.CopyTo(m_to, 0);
			m_rollShape.CopyFrom(m_shape);
			m_rollCover = cover;
			m_rollStretch = Stretch;
		}

		private void GoBack()
		{
			if (Showing)
			{
				if (m_reshaping)
				{
					m_now.CopyTo(m_to, 0);
					m_reshaping = false;
				}
				m_direction = -1;
			}
		}

		private static void Moved(Vector3[] a, Vector3[] b, out float mean, out float farthest)
		{
			float num = 0f;
			float num2 = 0f;
			for (int i = 0; i < a.Length; i++)
			{
				float magnitude = (a[i] - b[i]).magnitude;
				num += magnitude;
				num2 = Mathf.Max(num2, magnitude);
			}
			mean = num / (float)a.Length;
			farthest = num2;
		}

		private void Animate(MeshRenderer look)
		{
			if (!Showing)
			{
				if (m_object.activeSelf)
				{
					m_object.SetActive(value: false);
				}
				return;
			}
			if (m_renderer.sharedMaterial != look.sharedMaterial)
			{
				m_renderer.sharedMaterial = look.sharedMaterial;
				m_renderer.shadowCastingMode = look.shadowCastingMode;
				m_renderer.receiveShadows = look.receiveShadows;
				m_object.layer = look.gameObject.layer;
			}
			if (!m_object.activeSelf)
			{
				m_object.SetActive(value: true);
			}
			if (m_direction != 0)
			{
				bool flag = m_progress < 0.25f || (m_direction < 0 && m_progress <= 0.25f);
				float num = ((m_direction <= 0) ? (flag ? 0.3f : 0.7f) : (flag ? 0.35f : 1.1f));
				float num2 = (flag ? 0.25f : 0.75f);
				m_progress = Mathf.Clamp01(m_progress + (float)m_direction * Time.deltaTime * num2 / (num * 1f));
				Pose();
				Write(first: false, null, null);
				if (m_direction > 0 && m_progress >= 1f)
				{
					m_direction = 0;
				}
				else if (m_direction < 0 && m_progress <= 0f)
				{
					m_direction = 0;
					m_object.SetActive(value: false);
				}
			}
			else if (m_reshaping)
			{
				m_t = Mathf.Min(1f, m_t + Time.deltaTime / 1f);
				float t = Mathf.SmoothStep(0f, 1f, m_t);
				for (int i = 0; i < m_now.Length; i++)
				{
					m_now[i] = Vector3.LerpUnclamped(m_from[i], m_to[i], t);
				}
				Write(first: false, null, null);
				if (m_t >= 1f)
				{
					m_reshaping = false;
				}
			}
		}

		private void Pose()
		{
			float num = (m_sheet.MaxZ - m_sheet.MinZ) * m_rollStretch;
			float num2 = Mathf.Min(0.45f, num);
			if (m_progress <= 0.25f)
			{
				float num3 = m_progress / 0.25f;
				float t = Mathf.SmoothStep(0f, 1f, num3);
				Vector3 vector = Vector3.up * (Mathf.Sin(num3 * MathF.PI) * 0.2f);
				Drape.Roll(m_sheet, m_rollShape, in m_rollCover, m_rollStretch, m_to, num2, m_rolled);
				for (int i = 0; i < m_now.Length; i++)
				{
					m_now[i] = Vector3.LerpUnclamped(m_sheet.Rest[i], m_rolled[i], t) + vector;
				}
			}
			else
			{
				float num4 = Mathf.SmoothStep(0f, 1f, (m_progress - 0.25f) / 0.75f);
				Drape.Roll(m_sheet, m_rollShape, in m_rollCover, m_rollStretch, m_to, num2 + (num - num2) * num4, m_now);
			}
		}

		public void Rest()
		{
			m_measure?.Cancel();
			m_measure = null;
			m_wanted = false;
			m_player = null;
			if (Showing)
			{
				m_progress = 0f;
				m_direction = 0;
				m_reshaping = false;
				m_sheet.Rest.CopyTo(m_now, 0);
				Write(first: false, null, null);
				m_object.SetActive(value: false);
			}
		}

		private void Write(bool first, Vector2[] uv, int[] triangles)
		{
			int num = m_now.Length;
			for (int i = 0; i < num; i++)
			{
				m_normals[i] = Vector3.zero;
			}
			int[] triangles2 = m_sheet.Triangles;
			for (int j = 0; j < triangles2.Length; j += 3)
			{
				int num2 = triangles2[j];
				int num3 = triangles2[j + 1];
				int num4 = triangles2[j + 2];
				Vector3 vector = Vector3.Cross(m_now[num3] - m_now[num2], m_now[num4] - m_now[num2]);
				m_normals[num2] += vector;
				m_normals[num3] += vector;
				m_normals[num4] += vector;
			}
			for (int k = 0; k < num; k++)
			{
				Vector3 vector2 = ((m_normals[k].sqrMagnitude > 1E-12f) ? m_normals[k].normalized : Vector3.up);
				m_normals[k] = vector2;
				m_normals[num + k] = -vector2;
				m_vertices[k] = m_now[k];
				m_vertices[num + k] = m_now[k] - vector2 * 0.004f;
			}
			m_mesh.vertices = m_vertices;
			m_mesh.normals = m_normals;
			if (first)
			{
				m_mesh.uv = uv;
				m_mesh.triangles = triangles;
			}
			m_mesh.RecalculateTangents();
			m_mesh.RecalculateBounds();
		}

		public void Destroy(bool now)
		{
			m_measure?.Cancel();
			m_measure = null;
			if (now)
			{
				if (m_object != null)
				{
					UnityEngine.Object.DestroyImmediate(m_object);
				}
				UnityEngine.Object.DestroyImmediate(m_mesh);
			}
			else
			{
				if (m_object != null)
				{
					UnityEngine.Object.Destroy(m_object);
				}
				UnityEngine.Object.Destroy(m_mesh);
			}
		}

		public override string ToString()
		{
			return string.Format("side {0}'s fur: {1}{2}", m_side + 1, State, (m_player != null) ? (", over " + m_player.GetPlayerName()) : (m_wanted ? ", over nobody (forced)" : "")) + (Showing ? string.Format(", chest at z {0:F2}, stretched {1:F2}{2}", TopZ, Stretch, (Reshaped.Length > 0) ? (", " + Reshaped) : "") : "");
		}
	}
	internal sealed class Measure
	{
		private const float ArmorRoom = 0.04f;

		private const float Wait = 1f;

		private static readonly (string From, string To, float Radius)[] s_skeleton = new(string, string, float)[16]
		{
			("Hips", "Spine", 0.14f),
			("Spine", "Spine1", 0.13f),
			("Spine1", "Spine2", 0.13f),
			("Spine2", "Neck", 0.14f),
			("LeftUpLeg", "LeftLeg", 0.08f),
			("LeftLeg", "LeftFoot", 0.06f),
			("LeftFoot", "LeftToeBase", 0.045f),
			("RightUpLeg", "RightLeg", 0.08f),
			("RightLeg", "RightFoot", 0.06f),
			("RightFoot", "RightToeBase", 0.045f),
			("LeftArm", "LeftForeArm", 0.055f),
			("LeftForeArm", "LeftHand", 0.045f),
			("LeftHand", "LeftHandMiddle3_end", 0.04f),
			("RightArm", "RightForeArm", 0.055f),
			("RightForeArm", "RightHand", 0.045f),
			("RightHand", "RightHandMiddle3_end", 0.04f)
		};

		private static readonly Dictionary<SkinnedMeshRenderer, (GraphicsBuffer.Target Target, int Users)> s_opened = new Dictionary<SkinnedMeshRenderer, (GraphicsBuffer.Target, int)>();

		private static Mesh s_baked;

		private static readonly List<Vector3> s_points = new List<Vector3>();

		private readonly Field m_body;

		private readonly Transform m_bed;

		private readonly Dictionary<string, Transform> m_bones = new Dictionary<string, Transform>();

		private readonly List<SkinnedMeshRenderer> m_waiting = new List<SkinnedMeshRenderer>();

		private readonly StringBuilder m_report = new StringBuilder();

		private float m_neckZ = float.MinValue;

		private float m_topY = float.MaxValue;

		private const float Highest = 0.6f;

		private int m_pending;

		private float m_deadline;

		private bool m_closed;

		private bool m_skeletonDone;

		public float TopZ;

		private const float NearBones = 0.35f;

		private const float SmallestPiece = 0.1f;

		private const float LargestPiece = 3f;

		private List<(Vector3 A, Vector3 B)> m_boneSegments;

		public string Report => m_report.ToString();

		private Measure(Field body, Transform bed)
		{
			m_body = body;
			m_bed = bed;
		}

		public static Transform Bone(Player player, string name)
		{
			if (player == null || Access.Visual(player) == null)
			{
				return null;
			}
			Transform[] componentsInChildren = Access.Visual(player).GetComponentsInChildren<Transform>();
			foreach (Transform transform in componentsInChildren)
			{
				if (transform.name == name)
				{
					return transform;
				}
			}
			return null;
		}

		public static Measure Start(Player player, Transform bed, Field body, Vector3 spot)
		{
			float z = spot.z;
			Measure measure = new Measure(body, bed)
			{
				TopZ = z - 0.48f,
				m_topY = spot.y + 0.6f
			};
			body.Fill(float.MinValue);
			if (player == null || Access.Visual(player) == null)
			{
				measure.m_report.Append("nobody: flat on the bed");
				measure.m_closed = true;
				return measure;
			}
			Transform[] componentsInChildren = Access.Visual(player).GetComponentsInChildren<Transform>();
			foreach (Transform transform in componentsInChildren)
			{
				if (!measure.m_bones.ContainsKey(transform.name))
				{
					measure.m_bones[transform.name] = transform;
				}
			}
			if (measure.m_bones.TryGetValue("Neck", out var value))
			{
				measure.m_neckZ = measure.InBed(value.position).z;
			}
			if (measure.m_bones.TryGetValue("LeftArm", out var value2) && measure.m_bones.TryGetValue("RightArm", out var value3))
			{
				measure.TopZ = Mathf.Clamp((measure.InBed(value2.position).z + measure.InBed(value3.position).z) * 0.5f + 0.1f, z - 0.85f, z - 0.2f);
			}
			VisEquipment visEquipment = Access.VisEquip(player);
			List<Transform> list = new List<Transform>();
			if (visEquipment != null)
			{
				GameObject[] array = new GameObject[3] { Access.HairInstance(visEquipment), Access.BeardInstance(visEquipment), Access.HelmetInstance(visEquipment) };
				foreach (GameObject gameObject in array)
				{
					if (gameObject != null)
					{
						list.Add(gameObject.transform);
					}
				}
			}
			Renderer[] componentsInChildren2 = Access.Visual(player).GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in componentsInChildren2)
			{
				if (renderer.enabled && renderer.gameObject.activeInHierarchy && !list.Exists((Transform t) => renderer.transform.IsChildOf(t)))
				{
					if (renderer is SkinnedMeshRenderer skinned)
					{
						measure.Skinned(skinned);
					}
					else if (renderer is MeshRenderer plain)
					{
						measure.Plain(plain);
					}
				}
			}
			measure.m_deadline = Time.time + 1f;
			if (measure.m_pending == 0)
			{
				measure.m_closed = true;
			}
			return measure;
		}

		private Transform BoneOrNull(string name)
		{
			if (!m_bones.TryGetValue(name, out var value))
			{
				return null;
			}
			return value;
		}

		private Vector3 InBed(Vector3 world)
		{
			return m_bed.InverseTransformPoint(world);
		}

		private bool LeftOut(Vector3 p)
		{
			if (!(p.z < m_neckZ))
			{
				return p.y > m_topY;
			}
			return true;
		}

		private void Skinned(SkinnedMeshRenderer skinned)
		{
			Mesh sharedMesh = skinned.sharedMesh;
			if (sharedMesh == null)
			{
				return;
			}
			if (sharedMesh.isReadable)
			{
				if (s_baked == null)
				{
					s_baked = new Mesh
					{
						name = "DoubleBed_blanket_measure"
					};
				}
				skinned.BakeMesh(s_baked, useScale: true);
				s_baked.GetVertices(s_points);
				Points(skinned, s_points, "skinned here");
			}
			else if (!skinned.isVisible)
			{
				Skeleton(skinned.name, "not drawn now (hidden, or out of view)");
			}
			else if (SystemInfo.supportsAsyncGPUReadback)
			{
				Open(skinned);
				m_waiting.Add(skinned);
				m_pending++;
			}
			else
			{
				Skeleton(skinned.name, "cannot be read");
			}
		}

		private void Plain(MeshRenderer plain)
		{
			MeshFilter component;
			Mesh mesh = (plain.TryGetComponent<MeshFilter>(out component) ? component.sharedMesh : null);
			if (mesh == null)
			{
				return;
			}
			if (mesh.isReadable)
			{
				mesh.GetVertices(s_points);
				Splat(plain.localToWorldMatrix, s_points, plain.name, "read");
				return;
			}
			Bounds bounds = plain.bounds;
			Vector3 vector = Vector3.one * float.MaxValue;
			Vector3 vector2 = Vector3.one * float.MinValue;
			for (int i = 0; i < 8; i++)
			{
				Vector3 rhs = InBed(bounds.center + Vector3.Scale(bounds.extents, new Vector3((((uint)i & (true ? 1u : 0u)) != 0) ? 1 : (-1), (((uint)i & 2u) != 0) ? 1 : (-1), (((uint)i & 4u) != 0) ? 1 : (-1))));
				vector = Vector3.Min(vector, rhs);
				vector2 = Vector3.Max(vector2, rhs);
			}
			if (!LeftOut((vector + vector2) * 0.5f))
			{
				vector2.y = Mathf.Min(vector2.y, m_topY);
				m_body.Box(vector, vector2);
				m_report.AppendLine(plain.name + ": its box (cannot be read)");
			}
		}

		private void Points(SkinnedMeshRenderer skinned, List<Vector3> points, string how)
		{
			Transform transform = skinned.transform;
			Transform transform2 = ((skinned.rootBone != null) ? skinned.rootBone : transform);
			Matrix4x4[] array = new Matrix4x4[5]
			{
				transform.localToWorldMatrix,
				Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one),
				transform2.localToWorldMatrix,
				Matrix4x4.TRS(transform2.position, transform2.rotation, Vector3.one),
				Matrix4x4.identity
			};
			List<(Vector3, Vector3)> list = BoneSegments();
			if (list.Count == 0)
			{
				Skeleton(skinned.name, how + ", but there are no bones to check its points against");
				return;
			}
			int num = Mathf.Max(1, points.Count / 300);
			List<float> list2 = new List<float>(points.Count / num + 1);
			int num2 = -1;
			float num3 = float.MaxValue;
			for (int i = 0; i < array.Length; i++)
			{
				list2.Clear();
				Vector3 vector = Vector3.one * float.MaxValue;
				Vector3 vector2 = Vector3.one * float.MinValue;
				for (int j = 0; j < points.Count; j += num)
				{
					Vector3 vector3 = array[i].MultiplyPoint3x4(points[j]);
					vector = Vector3.Min(vector, vector3);
					vector2 = Vector3.Max(vector2, vector3);
					list2.Add(ToBones(vector3, list));
				}
				float magnitude = (vector2 - vector).magnitude;
				if (list2.Count != 0 && !(magnitude < 0.1f) && !(magnitude > 3f))
				{
					list2.Sort();
					float num4 = list2[list2.Count / 2];
					if (num4 < num3)
					{
						num3 = num4;
						num2 = i;
					}
				}
			}
			if (num2 < 0 || num3 > 0.35f)
			{
				Skeleton(skinned.name, $"{how}, but its points are not on the body ({num3 * 100f:F0} cm from the bones)");
			}
			else
			{
				Splat(array[num2], points, skinned.name, $"{how} (space {num2}, {num3 * 100f:F0} cm from the bones)");
			}
		}

		private List<(Vector3 A, Vector3 B)> BoneSegments()
		{
			if (m_boneSegments != null)
			{
				return m_boneSegments;
			}
			m_boneSegments = new List<(Vector3, Vector3)>();
			(string, string, float)[] array = s_skeleton;
			for (int i = 0; i < array.Length; i++)
			{
				(string, string, float) tuple = array[i];
				string item = tuple.Item1;
				string item2 = tuple.Item2;
				Transform transform = BoneOrNull(item);
				Transform transform2 = BoneOrNull(item2);
				if (transform != null && transform2 != null)
				{
					m_boneSegments.Add((transform.position, transform2.position));
				}
			}
			Transform transform3 = BoneOrNull("Neck");
			if ((object)transform3 != null)
			{
				Transform transform4 = BoneOrNull("Head");
				if ((object)transform4 != null)
				{
					m_boneSegments.Add((transform3.position, transform4.position));
				}
			}
			return m_boneSegments;
		}

		private static float ToBones(Vector3 p, List<(Vector3 A, Vector3 B)> bones)
		{
			float num = float.MaxValue;
			foreach (var bone in bones)
			{
				Vector3 item = bone.A;
				Vector3 vector = bone.B - item;
				float num2 = ((vector.sqrMagnitude > 1E-08f) ? Mathf.Clamp01(Vector3.Dot(p - item, vector) / vector.sqrMagnitude) : 0f);
				num = Mathf.Min(num, (item + vector * num2 - p).sqrMagnitude);
			}
			return Mathf.Sqrt(num);
		}

		private void Splat(Matrix4x4 toWorld, List<Vector3> points, string name, string how)
		{
			Matrix4x4 matrix4x = m_bed.worldToLocalMatrix * toWorld;
			int num = 0;
			foreach (Vector3 point in points)
			{
				Vector3 p = matrix4x.MultiplyPoint3x4(point);
				if (!float.IsNaN(p.x) && !LeftOut(p))
				{
					m_body.Point(p, 1);
					num++;
				}
			}
			m_report.AppendLine($"{name}: {how}, {num} of {points.Count} points");
		}

		private void Skeleton(string name, string why)
		{
			m_report.AppendLine(name + ": " + why + ": the skeleton stands in");
			if (m_skeletonDone)
			{
				return;
			}
			m_skeletonDone = true;
			(string, string, float)[] array = s_skeleton;
			for (int i = 0; i < array.Length; i++)
			{
				(string, string, float) tuple = array[i];
				string item = tuple.Item1;
				string item2 = tuple.Item2;
				float item3 = tuple.Item3;
				Transform transform = BoneOrNull(item);
				Transform transform2 = BoneOrNull(item2);
				if (!(transform == null) && !(transform2 == null))
				{
					Vector3 vector = InBed(transform.position);
					Vector3 vector2 = InBed(transform2.position);
					if (!LeftOut((vector + vector2) * 0.5f))
					{
						m_body.Capsule(vector, vector2, item3 + 0.04f);
					}
				}
			}
		}

		private static void Open(SkinnedMeshRenderer skinned)
		{
			if (s_opened.TryGetValue(skinned, out (GraphicsBuffer.Target, int) value))
			{
				s_opened[skinned] = (value.Item1, value.Item2 + 1);
				return;
			}
			s_opened[skinned] = (skinned.vertexBufferTarget, 1);
			skinned.vertexBufferTarget |= GraphicsBuffer.Target.CopySource | GraphicsBuffer.Target.Raw;
		}

		private static void Close(SkinnedMeshRenderer skinned)
		{
			if (!s_opened.TryGetValue(skinned, out (GraphicsBuffer.Target, int) value))
			{
				return;
			}
			if (value.Item2 > 1)
			{
				s_opened[skinned] = (value.Item1, value.Item2 - 1);
				return;
			}
			s_opened.Remove(skinned);
			if (skinned != null)
			{
				(skinned.vertexBufferTarget, _) = value;
			}
		}

		public bool Poll()
		{
			if (m_closed)
			{
				return true;
			}
			for (int num = m_waiting.Count - 1; num >= 0; num--)
			{
				SkinnedMeshRenderer skinned = m_waiting[num];
				GraphicsBuffer buffer = ((skinned != null) ? skinned.GetVertexBuffer() : null);
				if (buffer != null)
				{
					m_waiting.RemoveAt(num);
					AsyncGPUReadback.Request(buffer, delegate(AsyncGPUReadbackRequest request)
					{
						Arrived(skinned, buffer, request);
					});
				}
			}
			if (m_pending > 0 && Time.time < m_deadline)
			{
				return false;
			}
			foreach (SkinnedMeshRenderer item in m_waiting)
			{
				Close(item);
				Skeleton((item != null) ? item.name : "?", "its skinned vertices did not come");
			}
			m_waiting.Clear();
			if (m_pending > 0)
			{
				Skeleton("(still on the way)", "the graphics card did not answer in time");
			}
			m_closed = true;
			return true;
		}

		private void Arrived(SkinnedMeshRenderer skinned, GraphicsBuffer buffer, AsyncGPUReadbackRequest request)
		{
			buffer.Dispose();
			Close(skinned);
			if (m_closed || skinned == null)
			{
				return;
			}
			m_pending--;
			if (request.hasError)
			{
				Skeleton(skinned.name, "reading it back failed");
				return;
			}
			if (!skinned.isVisible)
			{
				Skeleton(skinned.name, "not drawn any more while it was read back (hidden, or out of view)");
				return;
			}
			NativeArray<float> data = request.GetData<float>();
			int num = ((skinned.sharedMesh != null) ? skinned.sharedMesh.vertexCount : 0);
			if (num == 0 || data.Length % num != 0 || data.Length / num < 3)
			{
				Skeleton(skinned.name, $"its vertex buffer is not laid out as expected ({data.Length} floats, {num} vertices)");
				return;
			}
			int num2 = data.Length / num;
			s_points.Clear();
			for (int i = 0; i < num; i++)
			{
				s_points.Add(new Vector3(data[i * num2], data[i * num2 + 1], data[i * num2 + 2]));
			}
			Points(skinned, s_points, "read back from the graphics card");
		}

		public void Cancel()
		{
			foreach (SkinnedMeshRenderer item in m_waiting)
			{
				Close(item);
			}
			m_waiting.Clear();
			m_closed = true;
		}

		public static void Uninstall()
		{
			foreach (KeyValuePair<SkinnedMeshRenderer, (GraphicsBuffer.Target, int)> item in s_opened)
			{
				if (item.Key != null)
				{
					item.Key.vertexBufferTarget = item.Value.Item1;
				}
			}
			s_opened.Clear();
			if (s_baked != null)
			{
				UnityEngine.Object.DestroyImmediate(s_baked);
			}
			s_baked = null;
		}
	}
	internal static class Patches
	{
		private enum Site
		{
			None,
			OwnerKey,
			OwnerNameKey,
			RpcName,
			NetView,
			ColliderRoot,
			AttachPoint
		}

		[HarmonyPatch]
		internal static class Bed_PerSide
		{
			private static IEnumerable<MethodBase> TargetMethods()
			{
				return Methods.Select(BedMethod);
			}

			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
			{
				foreach (CodeInstruction instruction in instructions)
				{
					switch (SiteOf(instruction, original.Name))
					{
					case Site.OwnerKey:
						yield return Moved(instruction, new CodeInstruction(OpCodes.Ldarg_0));
						yield return Call("OwnerKey");
						break;
					case Site.OwnerNameKey:
						yield return Moved(instruction, new CodeInstruction(OpCodes.Ldarg_0));
						yield return Call("OwnerNameKey");
						break;
					case Site.RpcName:
						yield return Moved(instruction, new CodeInstruction(OpCodes.Ldarg_0));
						yield return Call("RpcName");
						break;
					case Site.NetView:
						yield return Moved(instruction, Call("NetViewOf"));
						break;
					case Site.ColliderRoot:
						yield return Moved(instruction, Call("ColliderRoot"));
						break;
					case Site.AttachPoint:
						yield return Moved(instruction, Call("AttachPoint"));
						break;
					default:
						yield return instruction;
						break;
					}
				}
			}

			private static CodeInstruction Call(string name)
			{
				return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BedSides), name));
			}

			private static CodeInstruction Moved(CodeInstruction from, CodeInstruction to)
			{
				to.labels.AddRange(from.labels);
				to.blocks.AddRange(from.blocks);
				return to;
			}
		}

		[HarmonyPatch(typeof(Bed), "Awake")]
		internal static class Bed_Awake
		{
			private static void Postfix(Bed __instance)
			{
				BedSides.Wake(__instance);
				Blankets.Wake(__instance);
			}
		}

		[HarmonyPatch(typeof(WearNTear), "Destroy", new Type[]
		{
			typeof(HitData),
			typeof(bool)
		})]
		internal static class WearNTear_Destroy
		{
			private static void Prefix(WearNTear __instance)
			{
				Bed bed = BedSides.SecondSide(__instance.gameObject);
				if (bed != null && Access.WearView(__instance) != null && Access.WearView(__instance).IsOwner() && Game.instance != null)
				{
					Game.instance.RemoveCustomSpawnPoint(bed.GetSpawnPoint());
				}
			}
		}

		[HarmonyPatch(typeof(Bed), "Interact")]
		internal static class Bed_Interact
		{
			private static bool Prefix(Bed __instance, Humanoid human, bool repeat, bool alt, ref bool __result)
			{
				if (!alt || repeat || !BedSides.IsMyDragonSide(__instance))
				{
					return true;
				}
				bool flag = __instance.IsCurrent();
				Bed bed = BedSides.OtherSide(__instance);
				bool num = flag && BedSides.BothMine(__instance);
				Access.SetOwner(__instance, 0L, "");
				if (num && Game.instance != null)
				{
					Game.instance.GetPlayerProfile().SetCustomSpawnPoint(bed.m_spawnPoint.position);
				}
				else if (flag && Game.instance != null)
				{
					Game.instance.RemoveCustomSpawnPoint(Game.instance.GetPlayerProfile().GetCustomSpawnPoint());
				}
				human.Message(MessageHud.MessageType.Center, "Bed unclaimed");
				__result = false;
				return false;
			}
		}

		[HarmonyPatch(typeof(Bed), "GetSpawnPoint")]
		internal static class Bed_GetSpawnPoint
		{
			private static void Postfix(Bed __instance, ref Vector3 __result)
			{
				__result = BedSides.SpawnPoint(__instance, __result);
			}
		}

		[HarmonyPatch(typeof(Bed), "GetHoverText")]
		internal static class Bed_GetHoverText
		{
			private static void Postfix(Bed __instance, ref string __result)
			{
				if (BedSides.IsMyDragonSide(__instance))
				{
					string text = ((ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive()) ? "$KEY_AltKeys + $KEY_Use" : "$KEY_AltPlace + $KEY_Use");
					__result = __result + Localization.instance.Localize("\n[<color=yellow><b>" + text + "</b></color>] ") + "Unclaim";
				}
			}
		}

		private static readonly (string Method, Site Site, int Count)[] Expected = new(string, Site, int)[9]
		{
			("Awake", Site.NetView, 1),
			("Awake", Site.RpcName, 1),
			("SetOwner", Site.RpcName, 1),
			("RPC_SetOwner", Site.OwnerKey, 1),
			("RPC_SetOwner", Site.OwnerNameKey, 1),
			("GetOwner", Site.OwnerKey, 1),
			("GetOwnerName", Site.OwnerNameKey, 2),
			("Interact", Site.ColliderRoot, 1),
			("Interact", Site.AttachPoint, 1)
		};

		private static readonly Site[] Sites = new Site[6]
		{
			Site.OwnerKey,
			Site.OwnerNameKey,
			Site.RpcName,
			Site.NetView,
			Site.ColliderRoot,
			Site.AttachPoint
		};

		private static IEnumerable<string> Methods => Expected.Select(((string Method, Site Site, int Count) e) => e.Method).Distinct();

		private static MethodInfo BedMethod(string name)
		{
			return AccessTools.DeclaredMethod(typeof(Bed), name);
		}

		public static bool GameMatches(out string why)
		{
			foreach (string name in Methods)
			{
				MethodInfo methodInfo = BedMethod(name);
				if (methodInfo == null)
				{
					why = "no Bed." + name;
					return false;
				}
				List<CodeInstruction> originalInstructions = PatchProcessor.GetOriginalInstructions(methodInfo);
				Site[] sites = Sites;
				foreach (Site site in sites)
				{
					int num = originalInstructions.Count((CodeInstruction ins) => SiteOf(ins, name) == site);
					int num2 = Expected.Where(((string Method, Site Site, int Count) e) => e.Method == name && e.Site == site).Sum(((string Method, Site Site, int Count) e) => e.Count);
					if (num != num2)
					{
						why = $"Bed.{name}: {site} found {num} times, {num2} expected";
						return false;
					}
				}
			}
			why = null;
			return true;
		}

		private static Site SiteOf(CodeInstruction ins, string method)
		{
			if (ins.opcode == OpCodes.Ldsfld && ins.operand is FieldInfo fieldInfo && fieldInfo.DeclaringType == typeof(ZDOVars))
			{
				if (fieldInfo.Name == "s_owner")
				{
					return Site.OwnerKey;
				}
				if (fieldInfo.Name == "s_ownerName")
				{
					return Site.OwnerNameKey;
				}
			}
			if (ins.opcode == OpCodes.Ldstr && ins.operand as string == "SetOwner")
			{
				return Site.RpcName;
			}
			if (method == "Interact" && ins.opcode == OpCodes.Ldfld && ins.operand is FieldInfo fieldInfo2 && fieldInfo2.DeclaringType == typeof(Bed) && fieldInfo2.Name == "m_spawnPoint")
			{
				return Site.AttachPoint;
			}
			if ((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.operand is MethodInfo methodInfo && methodInfo.DeclaringType == typeof(Component))
			{
				if (method == "Awake" && methodInfo.Name == "GetComponent" && methodInfo.IsGenericMethod && methodInfo.GetGenericArguments()[0] == typeof(ZNetView))
				{
					return Site.NetView;
				}
				if (method == "Interact" && methodInfo.Name == "get_gameObject")
				{
					return Site.ColliderRoot;
				}
			}
			return Site.None;
		}
	}
	internal static class Runtime
	{
		public static void Install()
		{
			BedSides.Install();
			Blankets.Install();
		}

		public static void Uninstall()
		{
			Blankets.Uninstall();
			BedSides.Uninstall();
		}
	}

	internal static class DoubleDragonBed
	{
		internal const string OriginalModId = "hansenbansen.doublebed";

		internal static void Enable(Action<Type> patch)
		{
			if (!Patches.GameMatches(out var why))
			{
				Plugin.Log.LogError("Double dragon bed is disabled: the game's bed code changed (" + why + ").");
				return;
			}
			foreach (var type in typeof(Patches).GetNestedTypes(AccessTools.all))
				if (type.IsDefined(typeof(HarmonyPatch), false)) patch(type);
			Runtime.Install();
		}
	}
}
