using BepInEx;
using UnityEngine;
using System.Collections.Generic;

namespace PartyHatsMod
{
    [BepInPlugin("com.partyhats.mod", "Party Hats Mod", "1.0.0")]
    public class PartyHatsPlugin : BaseUnityPlugin
    {
        private PartyHatManager hatManager;

        private void Awake()
        {
            Logger.LogInfo("Party Hats Mod loaded");
        }

        private void Start()
        {
            var managerObject = new GameObject("PartyHatManager");
            hatManager = managerObject.AddComponent<PartyHatManager>();
            DontDestroyOnLoad(managerObject);
        }
    }

    public class PartyHatManager : MonoBehaviour
    {
        private readonly Dictionary<CellBody, PartyHat> activeHats = new();

        private void Update()
        {
            var allCells = CellBody.AllCells;
            if (allCells == null) return;

            // Add hats to any new cells
            foreach (var cell in allCells)
            {
                if (cell != null && !activeHats.ContainsKey(cell))
                {
                    activeHats[cell] = cell.gameObject.AddComponent<PartyHat>();
                }
            }

            // Clean up destroyed or null entries
            var deadEntries = new List<CellBody>();
            foreach (var entry in activeHats)
            {
                if (entry.Key == null || entry.Value == null)
                {
                    deadEntries.Add(entry.Key);
                }
            }

            foreach (var cell in deadEntries)
            {
                if (activeHats.TryGetValue(cell, out var hat) && hat != null)
                {
                    Destroy(hat.gameObject);
                }
                activeHats.Remove(cell);
            }
        }
    }

    public class PartyHat : MonoBehaviour
    {
        private CellBody attachedCell;
        private MeshFilter meshFilter;
        private LineRenderer coneRenderer;
        private LineRenderer pompomRenderer;
        private LineRenderer[] stripeRenderers;

        private float animationPhase;
        private float baseSize;
        private float originalRadius;
        private int colorPatternIndex;
        private int stripeCount;
        private Vector2 currentPosition;
        private Vector2 targetPosition;
        private bool isInitialized = false;

        private static readonly Color[][] ColorPalettes = {
            new[] { new Color(0.3f, 0.7f, 0.25f), new Color(0.9f, 0.35f, 0.55f), new Color(0.95f, 0.7f, 0.8f) },
            new[] { new Color(0.25f, 0.5f, 0.85f), new Color(0.95f, 0.85f, 0.2f), new Color(0.9f, 0.45f, 0.6f) },
            new[] { new Color(0.9f, 0.4f, 0.6f), new Color(0.98f, 0.98f, 0.98f), new Color(0.7f, 0.3f, 0.8f) },
            new[] { new Color(0.55f, 0.25f, 0.75f), new Color(0.2f, 0.7f, 0.65f), new Color(0.95f, 0.8f, 0.3f) },
            new[] { new Color(0.95f, 0.5f, 0.15f), new Color(1f, 0.97f, 0.9f), new Color(0.85f, 0.25f, 0.25f) },
            new[] { new Color(0.2f, 0.65f, 0.6f), new Color(0.95f, 0.55f, 0.5f), new Color(0.9f, 0.85f, 0.4f) },
        };

        private void Awake()
        {
            attachedCell = GetComponent<CellBody>();
            if (attachedCell == null)
            {
                Destroy(this);
                return;
            }

            meshFilter = GetComponent<MeshFilter>();
            animationPhase = Random.value * Mathf.PI * 2f;
            colorPatternIndex = Random.Range(0, ColorPalettes.Length);
            baseSize = Random.Range(0.4f, 0.6f);
            stripeCount = Random.Range(3, 6);

            var material = new Material(Shader.Find("UI/Default") ?? Shader.Find("Unlit/Transparent"));
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            var colors = ColorPalettes[colorPatternIndex];
            coneRenderer = CreateLineRenderer("Cone", 10, colors[0], material, flat: true);
            stripeRenderers = new LineRenderer[stripeCount];
            for (int i = 0; i < stripeCount; i++)
            {
                stripeRenderers[i] = CreateLineRenderer($"Stripe{i}", 11, colors[1], material);
            }
            pompomRenderer = CreateLineRenderer("Pompom", 12, colors[2], material);
        }

        private LineRenderer CreateLineRenderer(string name, int sortingOrder, Color color, Material material, bool flat = false)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(transform, worldPositionStays: false);
            var lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.sortingOrder = sortingOrder;
            lineRenderer.material = material;
            lineRenderer.numCornerVertices = 0;
            lineRenderer.numCapVertices = flat ? 0 : 4;
            lineRenderer.startColor = lineRenderer.endColor = color;
            return lineRenderer;
        }

        private void LateUpdate()
        {
            if (attachedCell == null || !attachedCell.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            float currentRadius = attachedCell.GetRadius();
            Vector2 center = transform.position;
            Vector2 hatBase = GetTopPoint(center, currentRadius);

            if (!isInitialized)
            {
                currentPosition = targetPosition = hatBase;
                originalRadius = currentRadius;
                isInitialized = true;
            }

            if (Vector2.Distance(hatBase, targetPosition) > currentRadius * 0.03f)
            {
                targetPosition = hatBase;
            }

            float speed = attachedCell.GetVelocity().magnitude;
            float lerpSpeed = Mathf.Lerp(8f, 15f, Mathf.Clamp01(speed * 0.5f));
            currentPosition = Vector2.Lerp(currentPosition, targetPosition, Time.deltaTime * lerpSpeed);

            animationPhase += Time.deltaTime * 1.5f;
            float wobble = Mathf.Sin(animationPhase) * 0.015f + speed * 0.005f * Mathf.Sin(animationPhase * 3f);

            RenderHat(currentPosition, currentRadius, wobble);
        }

        private Vector2 GetTopPoint(Vector2 center, float radius)
        {
            if (meshFilter?.sharedMesh == null)
            {
                return center + Vector2.up * radius;
            }

            Vector3 localTop = new Vector3(0, meshFilter.sharedMesh.bounds.max.y, 0);
            Vector3 worldTop = transform.TransformPoint(localTop);
            return new Vector2(worldTop.x, worldTop.y);
        }

        private void RenderHat(Vector2 basePosition, float radius, float wobbleOffset)
        {
            float growthFactor = 1f + Mathf.Clamp01((radius - originalRadius) / originalRadius) * 0.2f;
            float scaledSize = baseSize * growthFactor;

            float height = radius * scaledSize * 1.2f;
            float width = radius * scaledSize * 0.7f;
            float tiltAngle = Mathf.PI * 0.5f + wobbleOffset;

            Vector2 upwardDirection = new Vector2(Mathf.Cos(tiltAngle), Mathf.Sin(tiltAngle));
            Vector2 rightDirection = new Vector2(-upwardDirection.y, upwardDirection.x);
            Vector2 tipPosition = basePosition + upwardDirection * height;

            // Draw cone
            coneRenderer.positionCount = 2;
            coneRenderer.SetPosition(0, basePosition);
            coneRenderer.SetPosition(1, tipPosition);
            coneRenderer.startWidth = width;
            coneRenderer.endWidth = 0.01f;

            // Draw stripes
            float stripeThickness = Mathf.Clamp(radius * 0.04f, 0.015f, 0.06f);
            for (int i = 0; i < stripeCount; i++)
            {
                float stripeProgress = (i + 0.5f) / stripeCount;
                Vector2 stripeCenter = Vector2.Lerp(basePosition, tipPosition, stripeProgress);
                float stripeWidth = width * (1f - stripeProgress) * 0.5f;

                stripeRenderers[i].startWidth = stripeRenderers[i].endWidth = stripeThickness;
                stripeRenderers[i].positionCount = 2;
                stripeRenderers[i].SetPosition(0, stripeCenter + rightDirection * stripeWidth);
                stripeRenderers[i].SetPosition(1, stripeCenter - rightDirection * stripeWidth);
            }

            // Draw pompom
            float pompomRadius = radius * scaledSize * 0.18f;
            pompomRenderer.positionCount = 13;
            pompomRenderer.startWidth = pompomRenderer.endWidth = pompomRadius * 1.5f;
            for (int i = 0; i <= 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pompomRadius * 0.5f;
                pompomRenderer.SetPosition(i, tipPosition + offset);
            }
        }

        private void OnDestroy()
        {
            if (coneRenderer != null) Destroy(coneRenderer.gameObject);
            if (pompomRenderer != null) Destroy(pompomRenderer.gameObject);
            if (stripeRenderers != null)
            {
                foreach (var stripe in stripeRenderers)
                {
                    if (stripe != null) Destroy(stripe.gameObject);
                }
            }
        }
    }
}
