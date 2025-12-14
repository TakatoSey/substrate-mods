using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

namespace GameTweakerMod
{
    [BepInPlugin("com.gametweaker.mod", "Game Tweaker", "1.4.0")]
    public class GameTweakerPlugin : BaseUnityPlugin
    {
        private TweakerGUI tweakerGui;

        private void Awake()
        {
            Logger.LogInfo("Game Tweaker v1.4.0 loaded - Press F8 to toggle");
        }

        private void Start()
        {
            var guiObject = new GameObject("GameTweaker");
            tweakerGui = guiObject.AddComponent<TweakerGUI>();
            DontDestroyOnLoad(guiObject);
        }

        private void Update()
        {
            if (tweakerGui == null) return;
            if (Keyboard.current.f8Key.wasPressedThisFrame)
                tweakerGui.Toggle();
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                tweakerGui.TrySelectCellAtMouse();
        }
    }

    public class TweakerGUI : MonoBehaviour
    {
        private bool isVisible = false;
        private Rect windowArea = new Rect(20, 20, 420, 640);
        private Vector2 scrollPosition;
        private int currentTab = 0;
        private string[] tabNames = { "Global", "Spawn", "Cell", "Mutation" };

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle valueStyle;
        private GUIStyle panelStyle;
        private Texture2D backgroundTexture;
        private bool stylesReady = false;

        private CellBody currentCell;
        private int cellIndex = 0;

        private Dictionary<string, float> settings = new Dictionary<string, float>();
        private Dictionary<string, float> savedDefaults = new Dictionary<string, float>();
        private bool autoApply = true;
        private int previousCellCount = 0;
        private float updateTimer = 0f;
        private bool extractedFromGame = false;
        private string saveFilePath;

        private void Start()
        {
            backgroundTexture = new Texture2D(1, 1);
            backgroundTexture.SetPixel(0, 0, new Color(0.1f, 0.12f, 0.15f, 0.95f));
            backgroundTexture.Apply();
            saveFilePath = Path.Combine(BepInEx.Paths.ConfigPath, "GameTweaker_Defaults.txt");
            LoadSavedDefaults();
            SetupFallbackDefaults();
        }

        private void Update()
        {
            if (!extractedFromGame)
            {
                var allCells = CellBody.AllCells;
                if (allCells != null && allCells.Count > 0)
                    ExtractSettingsFromGame();
            }

            if (!autoApply) return;
            
            updateTimer += Time.deltaTime;
            var cells = CellBody.AllCells;
            int cellCount = cells?.Count ?? 0;
            
            if (cellCount > previousCellCount && updateTimer > 0.5f)
            {
                ApplyToAllCells();
                updateTimer = 0f;
            }
            previousCellCount = cellCount;
        }

        private void SetupFallbackDefaults()
        {
            var hardcodedDefaults = new Dictionary<string, float>
            {
                { "pinchTime", 0.7f },
                { "splitPause", 0.12f },
                { "polePullStrength", 0.6f },
                { "daughterOffsetFactor", 0.8f },
                { "cytosolDrainPerSecond", 0.002f },
                { "autoVacuoleToNucleusPerSecond", 1f },
                { "vacuoleTransferPerSecond", 6f },
                { "baseConversionRate", 2f },
                { "mass", 1f },
                { "linearDamping", 0.988f },
                { "growthSmoothing", 3.5f },
                { "mutationChance", 0.25f },
                { "minPercentDelta", 0.2f },
                { "maxPercentDelta", 0.6f },
                { "geneDuplicationMultiplier", 1f },
                { "geneDeletionMultiplier", 1f }
            };

            foreach (var kvp in hardcodedDefaults)
            {
                if (savedDefaults.ContainsKey(kvp.Key))
                    settings[kvp.Key] = savedDefaults[kvp.Key];
                else
                    settings[kvp.Key] = kvp.Value;
            }
        }

        private void LoadSavedDefaults()
        {
            savedDefaults.Clear();
            if (!File.Exists(saveFilePath)) return;

            try
            {
                var lines = File.ReadAllLines(saveFilePath);
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2 && float.TryParse(parts[1], out float value))
                        savedDefaults[parts[0]] = value;
                }
            }
            catch { }
        }

        private void SaveDefaults()
        {
            try
            {
                var lines = new List<string>();
                foreach (var kvp in savedDefaults)
                    lines.Add($"{kvp.Key}={kvp.Value}");
                File.WriteAllLines(saveFilePath, lines);
            }
            catch { }
        }

        private void ExtractSettingsFromGame()
        {
            if (extractedFromGame) return;

            var cells = CellBody.AllCells;
            if (cells == null || cells.Count == 0) return;

            var cell = cells[0];
            if (cell == null) return;

            settings["cytosolDrainPerSecond"] = ReadField<float>(cell, "cytosolDrainPerSecond");
            settings["autoVacuoleToNucleusPerSecond"] = ReadField<float>(cell, "autoVacuoleToNucleusPerSecond");
            settings["vacuoleTransferPerSecond"] = ReadField<float>(cell, "vacuoleTransferPerSecond");
            settings["mass"] = ReadField<float>(cell, "mass");
            settings["linearDamping"] = ReadField<float>(cell, "linearDamping");
            settings["growthSmoothing"] = ReadField<float>(cell, "growthSmoothing");

            var mitosisController = cell.GetComponent<MitosisController>();
            if (mitosisController != null)
            {
                settings["pinchTime"] = ReadField<float>(mitosisController, "pinchTime");
                settings["splitPause"] = ReadField<float>(mitosisController, "splitPause");
                settings["polePullStrength"] = ReadField<float>(mitosisController, "polePullStrength");
                settings["daughterOffsetFactor"] = ReadField<float>(mitosisController, "daughterOffsetFactor");

                var mutationField = typeof(MitosisController).GetField("geneMutationSettings", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mutationField != null)
                {
                    var mutationSettings = (GeneMutationSettings)mutationField.GetValue(mitosisController);
                    settings["mutationChance"] = mutationSettings.mutationChance;
                    settings["minPercentDelta"] = mutationSettings.minPercentDelta;
                    settings["maxPercentDelta"] = mutationSettings.maxPercentDelta;
                    settings["geneDuplicationMultiplier"] = mutationSettings.geneDuplicationMultiplier;
                    settings["geneDeletionMultiplier"] = mutationSettings.geneDeletionMultiplier;
                }
            }

            var mitochondria = cell.GetComponentsInChildren<Mitochondrion>();
            if (mitochondria.Length > 0)
                settings["baseConversionRate"] = ReadField<float>(mitochondria[0], "baseConversionRate");

            foreach (var kvp in settings)
                savedDefaults[kvp.Key] = kvp.Value;
            SaveDefaults();
            extractedFromGame = true;
        }

        private T ReadField<T>(object target, string fieldName)
        {
            if (target == null) return default;
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return default;
            return (T)field.GetValue(target);
        }

        private void WriteField(object target, string fieldName, float value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return;
            if (field.FieldType == typeof(int))
                field.SetValue(target, Mathf.RoundToInt(value));
            else
                field.SetValue(target, value);
        }

        private void SetupStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.3f, 1f, 0.8f);

            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            textStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleRight
            };
            valueStyle.normal.textColor = new Color(0.7f, 0.9f, 0.8f);

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = backgroundTexture;
        }

        private void OnGUI()
        {
            if (!isVisible) return;
            SetupStyles();
            windowArea = GUI.Window(98765, windowArea, RenderWindow, "", panelStyle);
        }

        private void RenderWindow(int windowId)
        {
            GUILayout.Space(5);
            GUILayout.Label("GAME TWEAKER", titleStyle);
            GUILayout.Space(3);
            RenderCellSelector();
            GUILayout.Space(3);
            currentTab = GUILayout.Toolbar(currentTab, tabNames);
            GUILayout.Space(8);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(480));

            switch (currentTab)
            {
                case 0: RenderGlobalTab(); break;
                case 1: RenderSpawnTab(); break;
                case 2: RenderCellTab(); break;
                case 3: RenderMutationTab(); break;
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, windowArea.width, 25));
        }

        private void RenderCellSelector()
        {
            var cells = CellBody.AllCells;
            int totalCells = cells?.Count ?? 0;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Cells: {totalCells}", textStyle, GUILayout.Width(60));

            if (GUILayout.Button("◄", GUILayout.Width(30))) SelectPreviousCell();

            string displayName = currentCell != null ? currentCell.name : "None";
            string indexDisplay = totalCells > 0 ? $"{cellIndex + 1}/{totalCells}" : "0/0";
            GUILayout.Label($"{displayName} ({indexDisplay})", textStyle);

            if (GUILayout.Button("►", GUILayout.Width(30))) SelectNextCell();
            GUILayout.EndHorizontal();

            if (currentCell != null)
            {
                float cytosol = currentCell.GetCytosolBuffer();
                float radius = currentCell.GetRadius();
                GUILayout.Label($"Cytosol: {cytosol:F2} | Radius: {radius:F2}", textStyle);
            }
            GUILayout.EndVertical();
        }

        private void SelectPreviousCell()
        {
            var cells = CellBody.AllCells;
            if (cells == null || cells.Count == 0) return;
            cellIndex = (cellIndex - 1 + cells.Count) % cells.Count;
            currentCell = cells[cellIndex];
        }

        private void SelectNextCell()
        {
            var cells = CellBody.AllCells;
            if (cells == null || cells.Count == 0) return;
            cellIndex = (cellIndex + 1) % cells.Count;
            currentCell = cells[cellIndex];
        }

        public void TrySelectCellAtMouse()
        {
            if (!isVisible) return;

            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            var mousePosition = Mouse.current.position.ReadValue();
            if (windowArea.Contains(new Vector2(mousePosition.x, Screen.height - mousePosition.y))) return;

            var worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, -mainCamera.transform.position.z));
            worldPosition.z = 0;

            var cells = CellBody.AllCells;
            if (cells == null) return;

            CellBody nearestCell = null;
            float nearestDistance = float.MaxValue;
            int nearestIndex = 0;

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;

                float distance = Vector2.Distance(worldPosition, cell.transform.position);
                if (distance <= cell.GetRadius() * 1.2f && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCell = cell;
                    nearestIndex = i;
                }
            }

            if (nearestCell != null)
            {
                currentCell = nearestCell;
                cellIndex = nearestIndex;
            }
        }

        private void EnsureValidCell()
        {
            var cells = CellBody.AllCells;
            if (cells == null || cells.Count == 0)
            {
                currentCell = null;
                cellIndex = 0;
                return;
            }

            if (currentCell == null || !currentCell.gameObject.activeInHierarchy)
            {
                cellIndex = Mathf.Clamp(cellIndex, 0, cells.Count - 1);
                currentCell = cells[cellIndex];
            }
            else
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i] == currentCell)
                    {
                        cellIndex = i;
                        break;
                    }
                }
            }
        }

        private void RenderGlobalTab()
        {
            GUILayout.Label("Global Settings", titleStyle);
            autoApply = GUILayout.Toggle(autoApply, "Auto-apply to all cells");
            GUILayout.Space(5);

            bool hasChanges = false;

            GUILayout.Label("Mitosis", titleStyle);
            hasChanges |= RenderSettingSlider("pinchTime", "Pinch Time", 0.1f, 3f);
            hasChanges |= RenderSettingSlider("splitPause", "Split Pause", 0.01f, 1f);
            hasChanges |= RenderSettingSlider("polePullStrength", "Pole Pull", 0f, 2f);
            hasChanges |= RenderSettingSlider("daughterOffsetFactor", "Daughter Offset", 0.2f, 2f);

            GUILayout.Label("Energy", titleStyle);
            hasChanges |= RenderSettingSlider("cytosolDrainPerSecond", "Cytosol Drain/s", 0f, 0.05f);
            hasChanges |= RenderSettingSlider("autoVacuoleToNucleusPerSecond", "Vac to Nuc Rate", 0f, 20f);
            hasChanges |= RenderSettingSlider("vacuoleTransferPerSecond", "Vacuole Transfer/s", 0f, 30f);
            hasChanges |= RenderSettingSlider("baseConversionRate", "Mito Conversion", 0f, 1f);

            GUILayout.Label("Movement", titleStyle);
            hasChanges |= RenderSettingSlider("mass", "Mass", 0.1f, 10f);
            hasChanges |= RenderSettingSlider("linearDamping", "Linear Damping", 0.9f, 1f);
            hasChanges |= RenderSettingSlider("growthSmoothing", "Growth Smoothing", 0.5f, 10f);

            GUILayout.Label("Mutation", titleStyle);
            hasChanges |= RenderSettingSlider("mutationChance", "Mutation Chance", 0f, 1f);
            hasChanges |= RenderSettingSlider("minPercentDelta", "Min Delta", 0f, 0.5f);
            hasChanges |= RenderSettingSlider("maxPercentDelta", "Max Delta", 0f, 1f);
            hasChanges |= RenderSettingSlider("geneDuplicationMultiplier", "Duplication Mult", 0f, 5f);
            hasChanges |= RenderSettingSlider("geneDeletionMultiplier", "Deletion Mult", 0f, 5f);

            if (hasChanges && autoApply)
                ApplyToAllCells();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Now")) ApplyToAllCells();
            if (GUILayout.Button("Reset Defaults"))
            {
                extractedFromGame = false;
                SetupFallbackDefaults();
                ExtractSettingsFromGame();
            }
            GUILayout.EndHorizontal();
        }

        private bool RenderSettingSlider(string key, string label, float minValue, float maxValue)
        {
            if (!settings.ContainsKey(key))
                settings[key] = 67f;

            float currentValue = settings[key];

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, textStyle, GUILayout.Width(140));
            float newValue = GUILayout.HorizontalSlider(currentValue, minValue, maxValue, GUILayout.Width(150));
            GUILayout.Label($"{newValue:F3}", valueStyle, GUILayout.Width(55));
            GUILayout.EndHorizontal();

            bool changed = Mathf.Abs(newValue - currentValue) > 0.0001f;
            settings[key] = newValue;
            return changed;
        }

        private void ApplyToAllCells()
        {
            var cells = CellBody.AllCells;
            if (cells == null) return;

            foreach (var cell in cells)
            {
                if (cell != null)
                    ApplySettingsToCell(cell);
            }
        }

        private void ApplySettingsToCell(CellBody cell)
        {
            WriteField(cell, "cytosolDrainPerSecond", settings["cytosolDrainPerSecond"]);
            WriteField(cell, "autoVacuoleToNucleusPerSecond", settings["autoVacuoleToNucleusPerSecond"]);
            WriteField(cell, "vacuoleTransferPerSecond", settings["vacuoleTransferPerSecond"]);
            WriteField(cell, "mass", settings["mass"]);
            WriteField(cell, "linearDamping", settings["linearDamping"]);
            WriteField(cell, "growthSmoothing", settings["growthSmoothing"]);

            var mitosisController = cell.GetComponent<MitosisController>();
            if (mitosisController != null)
            {
                WriteField(mitosisController, "pinchTime", settings["pinchTime"]);
                WriteField(mitosisController, "splitPause", settings["splitPause"]);
                WriteField(mitosisController, "polePullStrength", settings["polePullStrength"]);
                WriteField(mitosisController, "daughterOffsetFactor", settings["daughterOffsetFactor"]);

                var mutationField = typeof(MitosisController).GetField("geneMutationSettings", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mutationField != null)
                {
                    var mutationSettings = (GeneMutationSettings)mutationField.GetValue(mitosisController);
                    mutationSettings.mutationChance = settings["mutationChance"];
                    mutationSettings.minPercentDelta = settings["minPercentDelta"];
                    mutationSettings.maxPercentDelta = settings["maxPercentDelta"];
                    mutationSettings.geneDuplicationMultiplier = settings["geneDuplicationMultiplier"];
                    mutationSettings.geneDeletionMultiplier = settings["geneDeletionMultiplier"];
                    mutationField.SetValue(mitosisController, mutationSettings);
                }
            }

            foreach (var mito in cell.GetComponentsInChildren<Mitochondrion>())
            {
                if (mito != null)
                    WriteField(mito, "baseConversionRate", settings["baseConversionRate"]);
            }
        }

        private void RenderSpawnTab()
        {
            EnsureValidCell();
            GUILayout.Label("Add Genes", titleStyle);
            GUILayout.Space(5);

            if (currentCell == null)
            {
                GUILayout.Label("No cell selected", textStyle);
                return;
            }

            var organelles = currentCell.GetComponentsInChildren<Organelle>();
            int nucleusCount = 0;
            int vacuoleCount = 0;
            int mitoCount = 0;

            foreach (var organelle in organelles)
            {
                if (organelle == null) continue;
                switch (organelle.GetOrganelleType())
                {
                    case Organelle.OrganelleType.Nucleus: nucleusCount++; break;
                    case Organelle.OrganelleType.Vacuole: vacuoleCount++; break;
                    case Organelle.OrganelleType.Mitochondrion: mitoCount++; break;
                }
            }

            GUILayout.Label($"Organelles: {nucleusCount} Nuclei, {vacuoleCount} Vacuoles, {mitoCount} Mitos", textStyle);
            GUILayout.Space(10);

            GUILayout.Label("Add Genes (spawns organelle too)", titleStyle);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cilia")) AddGene("gene.cilia");
            if (GUILayout.Button("Collector")) AddGene("gene.collector");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Mitochondrion")) { AddGene("gene.mitochondrion"); SpawnMito(); }
            if (GUILayout.Button("Vacuole")) { AddGene("gene.vacuole"); SpawnVacuole(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Acid Enzyme")) AddGene("gene.acidenzyme");
            if (GUILayout.Button("Behavior")) AddGene("gene.behavior");
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Cytosol Capacity"))
                AddGene("gene.cytosol_capacity");

            GUILayout.Space(10);
            GUILayout.Label("Quick Actions", titleStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Mitosis"))
            {
                var mitosisController = currentCell.GetComponent<MitosisController>();
                if (mitosisController != null)
                    mitosisController.TryBegin(null, true);
            }
            if (GUILayout.Button("Fill Cytosol"))
                currentCell.AddCytosol(10f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 Cytosol")) currentCell.AddCytosol(1f);
            if (GUILayout.Button("-1 Cytosol")) currentCell.ConsumeCytosol(1f);
            GUILayout.EndHorizontal();
        }

        private void SpawnVacuole()
        {
            if (currentCell == null) return;

            var newObject = new GameObject("Vacuole_Spawned");
            newObject.transform.SetParent(currentCell.transform);
            newObject.transform.localPosition = Vector3.zero;

            var organelle = newObject.AddComponent<Organelle>();
            organelle.SetType(Organelle.OrganelleType.Vacuole);
            organelle.SetMovementEnabled(false);

            Vector2 position = UnityEngine.Random.insideUnitCircle * currentCell.GetRadius() * 0.2f;
            organelle.SetRelativePosition(position);
            organelle.RefreshParentReference();
        }

        private void SpawnMito()
        {
            if (currentCell == null) return;

            var newObject = new GameObject("Mito_Spawned");
            newObject.transform.SetParent(currentCell.transform);
            newObject.transform.localPosition = Vector3.zero;

            var organelle = newObject.AddComponent<Organelle>();
            organelle.SetType(Organelle.OrganelleType.Mitochondrion);
            newObject.AddComponent<Mitochondrion>();

            Vector2 position = UnityEngine.Random.insideUnitCircle * currentCell.GetRadius() * 0.15f;
            organelle.SetRelativePosition(position);
            organelle.RefreshParentReference();
            organelle.SetWorldRadius(currentCell.GetMitochondriaBaseRadius());
        }

        private void AddGene(string geneId)
        {
            if (currentCell == null) return;

            foreach (var organelle in currentCell.GetComponentsInChildren<Organelle>())
            {
                if (organelle != null && organelle.GetOrganelleType() == Organelle.OrganelleType.Nucleus)
                {
                    var existingGenes = organelle.GetGeneStates();
                    var updatedGenes = new List<GeneSerializedState>();

                    if (existingGenes != null)
                    {
                        foreach (var gene in existingGenes)
                            updatedGenes.Add(gene);
                    }

                    var newGene = GeneRegistry.CreateDefaultState(geneId);
                    if (!string.IsNullOrEmpty(newGene.GeneID))
                    {
                        updatedGenes.Add(newGene);
                        organelle.SetGenes(updatedGenes);
                    }
                    break;
                }
            }
        }

        private void RenderCellTab()
        {
            EnsureValidCell();
            GUILayout.Label("Cell Settings", titleStyle);
            GUILayout.Space(5);

            if (currentCell == null)
            {
                GUILayout.Label("No cell selected", textStyle);
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Energy", titleStyle);

            float cytosol = currentCell.GetCytosolBuffer();
            float cytosolMin = currentCell.GetCytosolMinBuffer();
            float cytosolMax = currentCell.GetEffectiveCytosolMaxBuffer();
            GUILayout.Label($"Cytosol: {cytosol:F2} ({cytosolMin:F2} - {cytosolMax:F2})", textStyle);

            RenderFieldSlider(currentCell, "cytosolDrainPerSecond", "Drain/s", 0f, 0.05f);
            RenderFieldSlider(currentCell, "autoVacuoleToNucleusPerSecond", "Vac to Nuc", 0f, 20f);
            RenderFieldSlider(currentCell, "vacuoleTransferPerSecond", "Vac Transfer", 0f, 30f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+0.5")) currentCell.AddCytosol(0.5f);
            if (GUILayout.Button("-0.5")) currentCell.ConsumeCytosol(0.5f);
            if (GUILayout.Button("+5")) currentCell.AddCytosol(5f);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Physics", titleStyle);
            RenderFieldSlider(currentCell, "mass", "Mass", 0.1f, 10f);
            RenderFieldSlider(currentCell, "linearDamping", "Damping", 0.9f, 1f);
            RenderFieldSlider(currentCell, "baseRadius", "Base Radius", 0.5f, 10f);
            RenderFieldSlider(currentCell, "growthSmoothing", "Growth Smooth", 0.5f, 10f);

            float speed = currentCell.GetVelocity().magnitude;
            GUILayout.Label($"Speed: {speed:F2} u/s", textStyle);
            GUILayout.EndVertical();

            var ciliaController = currentCell.GetComponent<CiliaController>();
            if (ciliaController != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Cilia", titleStyle);
                RenderFieldSlider(ciliaController, "ciliaLength", "Length", 0.1f, 2f);
                RenderFieldSlider(ciliaController, "waveSpeed", "Wave Speed", 0.5f, 10f);
                RenderFieldSlider(ciliaController, "waveAmp", "Amplitude", 0f, 1f);
                GUILayout.EndVertical();
            }

            var mitochondria = currentCell.GetComponentsInChildren<Mitochondrion>();
            if (mitochondria.Length > 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"Mitochondria ({mitochondria.Length})", titleStyle);
                RenderFieldSlider(mitochondria[0], "baseConversionRate", "Conversion", 0f, 1f);
                RenderFieldSlider(mitochondria[0], "baseCytosolRatio", "Cytosol Ratio", 0f, 2f);
                GUILayout.EndVertical();
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Focus Camera"))
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    var cellPosition = currentCell.transform.position;
                    mainCamera.transform.position = new Vector3(cellPosition.x, cellPosition.y, mainCamera.transform.position.z);
                }
            }
        }

        private void RenderFieldSlider(object target, string fieldName, string label, float minValue, float maxValue)
        {
            if (target == null) return;

            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                GUILayout.Label($"{label}: not found", textStyle);
                return;
            }

            float currentValue;
            if (field.FieldType == typeof(int))
                currentValue = (int)field.GetValue(target);
            else
                currentValue = (float)field.GetValue(target);

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, textStyle, GUILayout.Width(100));
            float newValue = GUILayout.HorizontalSlider(currentValue, minValue, maxValue, GUILayout.Width(150));
            GUILayout.Label($"{newValue:F2}", valueStyle, GUILayout.Width(45));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(newValue - currentValue) > 0.001f)
            {
                if (field.FieldType == typeof(int))
                    field.SetValue(target, Mathf.RoundToInt(newValue));
                else
                    field.SetValue(target, newValue);
            }
        }

        private void RenderMutationTab()
        {
            EnsureValidCell();
            GUILayout.Label("Mutation Settings", titleStyle);
            GUILayout.Space(5);

            if (currentCell == null)
            {
                GUILayout.Label("No cell selected", textStyle);
                return;
            }

            var mitosisController = currentCell.GetComponent<MitosisController>();
            if (mitosisController == null)
            {
                GUILayout.Label("Cell has no MitosisController", textStyle);
                return;
            }

            var mutationField = typeof(MitosisController).GetField("geneMutationSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mutationField == null)
            {
                GUILayout.Label("Cannot access mutation settings", textStyle);
                return;
            }

            var mutationSettings = (GeneMutationSettings)mutationField.GetValue(mitosisController);

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mutation Chance", textStyle, GUILayout.Width(130));
            mutationSettings.mutationChance = GUILayout.HorizontalSlider(mutationSettings.mutationChance, 0f, 1f, GUILayout.Width(150));
            GUILayout.Label($"{mutationSettings.mutationChance:F2}", valueStyle, GUILayout.Width(45));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Min Delta", textStyle, GUILayout.Width(130));
            mutationSettings.minPercentDelta = GUILayout.HorizontalSlider(mutationSettings.minPercentDelta, 0f, 0.5f, GUILayout.Width(150));
            GUILayout.Label($"{mutationSettings.minPercentDelta:F2}", valueStyle, GUILayout.Width(45));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max Delta", textStyle, GUILayout.Width(130));
            mutationSettings.maxPercentDelta = GUILayout.HorizontalSlider(mutationSettings.maxPercentDelta, 0f, 1f, GUILayout.Width(150));
            GUILayout.Label($"{mutationSettings.maxPercentDelta:F2}", valueStyle, GUILayout.Width(45));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Duplication Mult", textStyle, GUILayout.Width(130));
            mutationSettings.geneDuplicationMultiplier = GUILayout.HorizontalSlider(mutationSettings.geneDuplicationMultiplier, 0f, 5f, GUILayout.Width(150));
            GUILayout.Label($"{mutationSettings.geneDuplicationMultiplier:F2}", valueStyle, GUILayout.Width(45));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Deletion Mult", textStyle, GUILayout.Width(130));
            mutationSettings.geneDeletionMultiplier = GUILayout.HorizontalSlider(mutationSettings.geneDeletionMultiplier, 0f, 5f, GUILayout.Width(150));
            GUILayout.Label($"{mutationSettings.geneDeletionMultiplier:F2}", valueStyle, GUILayout.Width(45));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            mutationSettings.valueWeighted = GUILayout.Toggle(mutationSettings.valueWeighted, "Value Weighted");
            mutationSettings.allowMultiVariableMutation = GUILayout.Toggle(mutationSettings.allowMultiVariableMutation, "Multi-Variable Mutation");

            mutationField.SetValue(mitosisController, mutationSettings);
            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Gene Ranges", titleStyle);
            GUILayout.Label("Mito Efficiency: 0.4 - 5.0", textStyle);
            GUILayout.Label("Vacuole Capacity: 0.5 - 3.0", textStyle);
            GUILayout.Label("Cilia Thrust/Speed: 0.35 - 2.5", textStyle);
            GUILayout.Label("Collector Capture: 0.6 - 1.6", textStyle);
            GUILayout.EndVertical();
        }

        public void Toggle()
        {
            isVisible = !isVisible;
        }

        private void OnDestroy()
        {
            if (backgroundTexture != null)
                Destroy(backgroundTexture);
        }
    }
}
