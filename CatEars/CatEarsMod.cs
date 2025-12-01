using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace CatEarsMod
{
    [BepInPlugin("com.catears.mod", "Cat Ears Mod", "1.0.0")]
    public class CuteCellsPlugin : BaseUnityPlugin
    {
        private CatEarManager earManager;

        private void Awake()
        {
            Logger.LogInfo("Cat Ears Mod v1");
        }

        private void Start()
        {
            var obj = new GameObject("CatEarManager");
            earManager = obj.AddComponent<CatEarManager>();
            DontDestroyOnLoad(obj);
        }

        private void Update()
        {
            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                earManager?.ToggleEars();
            }
        }
    }

    public class CatEarManager : MonoBehaviour
    {
        private bool earsEnabled = true;
        private readonly Dictionary<CellBody, CatEars> activeEars = new();

        private void Update()
        {
            var allCells = CellBody.AllCells;
            if (allCells == null) return;

            // Add ears to new cells
            if (earsEnabled)
            {
                foreach (var cell in allCells)
                {
                    if (cell == null) continue;
                    if (!activeEars.ContainsKey(cell))
                    {
                        var ears = cell.gameObject.AddComponent<CatEars>();
                        activeEars[cell] = ears;
                    }
                }
            }

            // Clean up destroyed cells
            var toRemove = new List<CellBody>();
            foreach (var kvp in activeEars)
            {
                if (kvp.Key == null || kvp.Value == null)
                    toRemove.Add(kvp.Key);
            }

            foreach (var cell in toRemove)
            {
                if (activeEars[cell] != null)
                    Destroy(activeEars[cell].gameObject);
                activeEars.Remove(cell);
            }
        }

        public void ToggleEars()
        {
            earsEnabled = !earsEnabled;

            if (!earsEnabled)
            {
                // Remove all ears
                foreach (var ears in activeEars.Values)
                {
                    if (ears != null)
                        Destroy(ears.gameObject);
                }
                activeEars.Clear();
                Debug.Log("Cat ears disabled :(");
            }
            else
            {
                Debug.Log("Cat ears enabled :3");
            }
        }
    }

    public class CatEars : MonoBehaviour
    {
        private CellBody cellBody;
        private LineRenderer leftOutline, rightOutline;
        private LineRenderer leftInner, rightInner;

        private float wigglePhase;
        private static readonly Color OuterColor = new(1f, 0.4f, 0.7f, 0.95f);// Pink
        private static readonly Color InnerColor = new(1f, 0.7f, 0.85f, 0.9f);// Light pink

        private void Awake()
        {
            cellBody = GetComponent<CellBody>();
            if (cellBody == null)
            {
                Destroy(this);
                return;
            }

            wigglePhase = Random.value * Mathf.PI * 2f;
            CreateEars();
        }

        private void CreateEars()
        {
            var material = new Material(Shader.Find("UI/Default") ?? Shader.Find("Unlit/Transparent"));
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            leftOutline = CreateEarLine("LeftEarOutline", 10, OuterColor, material);
            leftInner = CreateEarLine("LeftEarInner", 9, InnerColor, material);
            rightOutline = CreateEarLine("RightEarOutline", 10, OuterColor, material);
            rightInner = CreateEarLine("RightEarInner", 9, InnerColor, material);
        }

        private LineRenderer CreateEarLine(string name, int sortingOrder, Color color, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sortingOrder = sortingOrder;
            lr.material = mat;
            lr.loop = true;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) }
            };
            return lr;
        }

        private void Update()
        {
            if (cellBody == null || !cellBody.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            float radius = cellBody.GetRadius();
            Vector2 pos = transform.position;

            wigglePhase += Time.deltaTime * 2.5f;
            float baseWiggle = Mathf.Sin(wigglePhase) * 0.06f;
            float speedWiggle = cellBody.GetVelocity().magnitude * 0.02f * Mathf.Sin(wigglePhase * 5f);

            UpdateEar(leftOutline, leftInner, pos, radius, Mathf.PI * 0.5f + 0.4f + baseWiggle + speedWiggle, true);
            UpdateEar(rightOutline, rightInner, pos, radius, Mathf.PI * 0.5f - 0.4f - baseWiggle - speedWiggle, false);
        }

        private void UpdateEar(LineRenderer outline, LineRenderer inner, Vector2 pos, float radius, float angle, bool isLeft)
        {
            float height = radius * 0.45f;
            float width = radius * 0.28f;

            Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 perp = new(-dir.y, dir.x); // perpendicular

            Vector2 basePos = pos + dir * (radius * 0.95f);
            Vector2 tip = basePos + dir * height;

            // Outline triangle
            outline.positionCount = 4;
            outline.SetPosition(0, basePos + perp * width * 0.5f);
            outline.SetPosition(1, tip);
            outline.SetPosition(2, basePos - perp * width * 0.5f);
            outline.SetPosition(3, basePos + perp * width * 0.5f);

            // Inner triangle
            Vector2 innerBase = basePos + dir * (height * 0.15f);
            Vector2 innerTip = innerBase + dir * (height * 0.6f);
            inner.positionCount = 3;
            inner.SetPosition(0, innerBase + perp * width * 0.25f);
            inner.SetPosition(1, innerTip);
            inner.SetPosition(2, innerBase - perp * width * 0.25f);

            // Scale width
            float w = Mathf.Clamp(radius * 0.08f, 0.03f, 0.12f);
            outline.startWidth = outline.endWidth = w;
            inner.startWidth = w * 2f;
            inner.endWidth = w;
        }

        private void OnDestroy()
        {
            Destroy(leftOutline?.gameObject);
            Destroy(rightOutline?.gameObject);
        }
    }
}

