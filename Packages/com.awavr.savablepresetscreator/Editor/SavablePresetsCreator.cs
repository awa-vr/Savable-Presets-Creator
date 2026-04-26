using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;
using AnimatorLayerBlendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode;
using Object = UnityEngine.Object;

namespace AwAVR.SavablePresetsCreator
{
    public class SavablePresetsCreator : EditorWindow
    {
        #region Variables

        private static string _windowTitle = "Savable Presets Creator";

        private const string WildcardSuffix = "/*";
        private const char ExcludePrefix = '!';
        private List<VRCAvatarDescriptor> _avatars;
        private VRCAvatarDescriptor _avatar;
        private AnimatorController _fx;
        private SavablePresetConfiguration _configuration;

        private Vector2 _scrollPos = Vector2.zero;
        private Dictionary<SavablePreset, bool> _parameterFoldouts = new Dictionary<SavablePreset, bool>();

        private Dictionary<SavablePreset, ReorderableList> _reorderableLists =
            new Dictionary<SavablePreset, ReorderableList>();

        #endregion

        #region Window

        [MenuItem("Tools/AwA/Savable Preset Creator", false, -100)]
        public static void ShowWindow()
        {
            var window = GetWindow<SavablePresetsCreator>(_windowTitle);
            window.titleContent = new GUIContent(
                image: EditorGUIUtility.IconContent("d_Audio Mixer@2x").image,
                text: _windowTitle,
                tooltip: "Create in-game savable presets."
            );
            window.minSize = new Vector2(450f, window.minSize.y);
        }

        public static void OpenWithConfiguration(SavablePresetConfiguration configuration)
        {
            ShowWindow();
            if (configuration != null)
            {
                var window = GetWindow<SavablePresetsCreator>(_windowTitle);
                window._configuration = configuration;
            }
            else
            {
                Debug.LogError("Given configuration was invalid!");
            }
        }

        public void OnEnable()
        {
            _avatars = Core.GetAvatarsInScene() ?? new List<VRCAvatarDescriptor>();

            if (_avatars.Count == 1)
            {
                _avatar = _avatars.First();
                _avatars.Clear();
            }
        }

        public void OnGUI()
        {
            Core.Title(_windowTitle);

            if (!GetAvatar())
                return;

            if (!GetFXController())
                return;

            DrawConfigurationField();
            if (_configuration)
            {
                CheckController();
                CheckVRCParameters();
                CheckSavablePresets();

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _configuration.Name = EditorGUILayout.TextField("Configuration Name:", _configuration.Name);
                    using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
                    {
                        DrawSavablePresets();
                        _scrollPos = scrollView.scrollPosition;
                    }

                    if (GUILayout.Button("Add Preset"))
                    {
                        Undo.RecordObject(_configuration, "Add Preset");
                        _configuration.SavablePresets.Add(new SavablePreset { Name = "New Preset" });
                        EditorUtility.SetDirty(_configuration);
                    }
                }

                if (GUILayout.Button("Update Animator"))
                {
                    UpdateAnimator();
                }
            }
            else
            {
                DrawNewConfigurationPopup();
            }
        }

        #endregion

        #region GUIHelpers

        private bool GetAvatar()
        {
            Core.GetAvatar(ref _avatar, ref _avatars);
            if (!_avatar)
            {
                EditorGUILayout.HelpBox("Please select an avatar", MessageType.Error);
                return false;
            }

            if (_avatar.expressionParameters == null)
            {
                EditorGUILayout.HelpBox(
                    "Selected avatar has no Expression Parameters asset. Wildcard preview/expansion will only include exact names.",
                    MessageType.Warning);
            }

            return true;
        }

        private bool GetFXController()
        {
            _fx = Core.GetAnimatorController(_avatar);

            if (_fx == null)
            {
                EditorGUILayout.HelpBox("Can't find an FX animator on your avatar. Please add one", MessageType.Error);
                return false;
            }

            return true;
        }

        private void DrawConfigurationField()
        {
            _configuration = EditorGUILayout.ObjectField(
                    "Configuration",
                    _configuration,
                    typeof(SavablePresetConfiguration),
                    false)
                as SavablePresetConfiguration;
        }

        private void DrawNewConfigurationPopup()
        {
            EditorGUILayout.HelpBox("No configuration selected!" +
                                    "\nPlease select one or create one to use this tool." +
                                    "\n" +
                                    "\nEither use the button below to create a new configuration in the Assets folder," +
                                    "\nor right-click in a folder (Create/AwA/Savable Preset Configuration)",
                MessageType.Warning);
            if (GUILayout.Button("Create new configuration"))
            {
                string path = "Assets/New Savable Preset Configuration.asset";
                path = AssetDatabase.GenerateUniqueAssetPath(path);

                var newConfiguration = ScriptableObject.CreateInstance<SavablePresetConfiguration>();
                newConfiguration.Name = "New Configuration";

                AssetDatabase.CreateAsset(newConfiguration, path);
                AssetDatabase.SaveAssets();

                _configuration = newConfiguration;
                EditorGUIUtility.PingObject(newConfiguration);
                Selection.activeObject = newConfiguration;
            }
        }

        private void DrawSavablePresetHeader(ref SavablePreset preset, ref int i)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField(preset.Name);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_configuration, "Rename Preset");
                    preset.Name = newName;
                    EditorUtility.SetDirty(_configuration);
                }

                if (GUILayout.Button("Duplicate", GUILayout.Width(70)))
                {
                    Undo.RecordObject(_configuration, "Duplicate Preset");
                    var newPreset = new SavablePreset
                    {
                        Name = preset.Name + " (Copy)",
                        Parameters = new List<string>(preset.Parameters)
                    };
                    _configuration.SavablePresets.Insert(i + 1, newPreset);
                    EditorUtility.SetDirty(_configuration);
                }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    Undo.RecordObject(_configuration, "Remove Preset");
                    _configuration.SavablePresets.RemoveAt(i);
                    EditorUtility.SetDirty(_configuration);
                    i--; // Adjust index since we removed an item
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load"))
                {
                    CopyPath(preset.Name, "Load");
                }

                if (GUILayout.Button("Reset"))
                {
                    CopyPath(preset.Name, "Reset");
                }

                if (GUILayout.Button("Save"))
                {
                    CopyPath(preset.Name, "Save");
                }

                if (GUILayout.Button("Preview Added"))
                {
                    ShowPreviewAddedParameters(preset);
                }
            }
        }

        private void ShowPreviewAddedParameters(SavablePreset preset)
        {
            var expanded = ResolvePresetParameters(preset, out var unmatchedWildcards);
            var builder = new StringBuilder();

            builder.AppendLine($"Preset: {preset.Name}");
            builder.AppendLine($"Resolved Parameters: {expanded.Count}");
            builder.AppendLine();

            if (expanded.Count == 0)
            {
                builder.AppendLine("(No parameters resolved)");
            }
            else
            {
                foreach (var parameter in expanded)
                {
                    builder.AppendLine(parameter);
                }
            }

            if (unmatchedWildcards.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Unmatched wildcard patterns:");
                foreach (var wildcard in unmatchedWildcards)
                {
                    builder.AppendLine($"- {wildcard}");
                }
            }

            EditorUtility.DisplayDialog("Preview Added Parameters", builder.ToString(), "Close");
        }

        private void CopyPath(string presetName, string parameter)
        {
            string path = JoinParameterPath("SA", presetName);
            path = JoinParameterPath(path, parameter);
            EditorGUIUtility.systemCopyBuffer = path;
        }

        private void DrawSavablePresets()
        {
            for (int i = 0; i < _configuration.SavablePresets.Count; i++)
            {
                var preset = _configuration.SavablePresets[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSavablePresetHeader(ref preset, ref i);

                    if (!_parameterFoldouts.ContainsKey(preset))
                    {
                        _parameterFoldouts[preset] = false;
                    }

                    _parameterFoldouts[preset] =
                        EditorGUILayout.Foldout(_parameterFoldouts[preset], "Parameters", true);

                    if (_parameterFoldouts[preset])
                    {
                        DrawParameters(ref preset);
                    }
                }

                GUILayout.Space(10);
            }
        }

        private void DrawParameters(ref SavablePreset preset)
        {
            if (preset.Parameters == null)
                preset.Parameters = new List<string>();

            int newCount = EditorGUILayout.DelayedIntField("Size", preset.Parameters.Count);
            if (newCount != preset.Parameters.Count && newCount >= 0)
            {
                Undo.RecordObject(_configuration, "Resize Parameters");

                while (preset.Parameters.Count < newCount)
                    preset.Parameters.Add(string.Empty);

                while (preset.Parameters.Count > newCount)
                    preset.Parameters.RemoveAt(preset.Parameters.Count - 1);

                EditorUtility.SetDirty(_configuration);
            }

            if (!_reorderableLists.TryGetValue(preset, out var list))
            {
                var p = preset;
                list = new ReorderableList(p.Parameters, typeof(string), true, false, true, true);

                list.drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = p.Parameters[index];
                    rect.y += 2;

                    var nameRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);

                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUI.TextField(nameRect, element);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_configuration, "Rename Parameter");
                        p.Parameters[index] = newName;
                        EditorUtility.SetDirty(_configuration);
                    }
                };

                list.onAddCallback = (l) =>
                {
                    Undo.RecordObject(_configuration, "Add Parameter");
                    p.Parameters.Add(string.Empty);
                    EditorUtility.SetDirty(_configuration);
                };

                list.onRemoveCallback = (l) =>
                {
                    Undo.RecordObject(_configuration, "Remove Parameter");
                    p.Parameters.RemoveAt(l.index);
                    EditorUtility.SetDirty(_configuration);
                };

                list.headerHeight = 1;
                _reorderableLists[preset] = list;
            }

            // Ensure the list reference is up to date (e.g. after Undo)
            list.list = preset.Parameters;
            list.DoLayoutList();
        }

        #endregion

        #region Methods

        private void CheckController()
        {
            if (!_configuration.Controller)
            {
                // Create Controller
                string configPath = AssetDatabase.GetAssetPath(_configuration);
                string configDirectory = System.IO.Path.GetDirectoryName(configPath);
                string path = System.IO.Path.Combine(configDirectory, "New Savable Presets Animator.controller");
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

                _configuration.Controller = controller;
            }
        }

        private void CheckVRCParameters()
        {
            if (!_configuration.VRCParameters)
            {
                // Create new VRC Expression Parameters
                string configPath = AssetDatabase.GetAssetPath(_configuration);
                string configDirectory = System.IO.Path.GetDirectoryName(configPath);
                string path = System.IO.Path.Combine(configDirectory, "New Savable Presets Parameters.asset");
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                var expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                AssetDatabase.CreateAsset(expressionParameters, path);

                _configuration.VRCParameters = expressionParameters;
            }
        }

        private void CheckSavablePresets()
        {
            if (_configuration.SavablePresets == null)
                _configuration.SavablePresets = new List<SavablePreset>();
        }

        private void UpdateAnimator()
        {
            if (_configuration.Controller == null)
                return;

            var dirtyObjects = new Object[] { _configuration.Controller, _configuration.VRCParameters };
            Undo.RecordObjects(dirtyObjects, "Update Savable Presets Animator");

            // Clear Animator
            _configuration.Controller.layers = Array.Empty<AnimatorControllerLayer>();
            _configuration.Controller.parameters = Array.Empty<AnimatorControllerParameter>();

            _configuration.Controller.AddLayer("Base Layer");
            _configuration.Controller.AddLayer("AwA - Savable Presets - DO NOT TOUCH");

            // Clear VRC Parameters
            _configuration.VRCParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();

            // Create
            var layers = _configuration.Controller.layers;
            var layerIndex = Array.FindIndex(layers, l => l.name == "AwA - Savable Presets - DO NOT TOUCH");

            if (layerIndex != -1)
            {
                layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
                layers[layerIndex].defaultWeight = 0.0f;
                _configuration.Controller.layers = layers;
            }

            var animatorLayer = Core.GetLayerByName(_configuration.Controller, "Savable Presets");
            var idleState = animatorLayer.stateMachine.AddState("Idle");

            foreach (var configurationSavablePreset in _configuration.SavablePresets)
            {
                CreateSavablePreset(configurationSavablePreset, ref animatorLayer, ref idleState);
            }

            Core.CleanObjects(dirtyObjects);
        }

        private string JoinParameterPath(string a, string b)
        {
            return string.Join('/', new string[] { a, b });
        }

        private List<string> GetAvatarParameterNames()
        {
            if (_avatar == null || _avatar.expressionParameters == null || _avatar.expressionParameters.parameters == null)
                return new List<string>();

            return _avatar.expressionParameters.parameters
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.name))
                .Select(p => p.name.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private List<string> ResolvePresetParameters(SavablePreset preset, out List<string> unmatchedWildcards)
        {
            unmatchedWildcards = new List<string>();
            var resolved = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var avatarParameters = GetAvatarParameterNames();
            var exactExclusions = new HashSet<string>(StringComparer.Ordinal);
            var wildcardExclusionPrefixes = new List<string>();

            if (preset?.Parameters == null)
                return resolved;

            bool IsWildcard(string value)
            {
                return value.EndsWith(WildcardSuffix, StringComparison.Ordinal);
            }

            bool IsExcluded(string value)
            {
                if (exactExclusions.Contains(value))
                    return true;

                return wildcardExclusionPrefixes.Any(prefix =>
                    value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }

            void RemoveResolvedMatches(Func<string, bool> predicate)
            {
                resolved.RemoveAll(item =>
                {
                    if (!predicate(item))
                        return false;

                    seen.Remove(item);
                    return true;
                });
            }

            foreach (var rawEntry in preset.Parameters)
            {
                if (string.IsNullOrWhiteSpace(rawEntry))
                    continue;

                var entry = rawEntry.Trim();

                // Prefixing with "!" excludes exact names and wildcards (e.g. !Color/*).
                if (entry.StartsWith(ExcludePrefix.ToString(), StringComparison.Ordinal))
                {
                    var excludedEntry = entry.Substring(1).Trim();
                    if (string.IsNullOrWhiteSpace(excludedEntry))
                        continue;

                    if (IsWildcard(excludedEntry))
                    {
                        var exclusionPrefix = excludedEntry.Substring(0, excludedEntry.Length - WildcardSuffix.Length);
                        wildcardExclusionPrefixes.Add(exclusionPrefix);
                        RemoveResolvedMatches(item =>
                            item.StartsWith(exclusionPrefix, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        exactExclusions.Add(excludedEntry);
                        RemoveResolvedMatches(item => string.Equals(item, excludedEntry, StringComparison.Ordinal));
                    }

                    continue;
                }

                if (!IsWildcard(entry))
                {
                    if (!IsExcluded(entry) && seen.Add(entry))
                        resolved.Add(entry);
                    continue;
                }

                // "Color/*" expands by matching avatar parameters with prefix "Color".
                var wildcardPrefix = entry.Substring(0, entry.Length - WildcardSuffix.Length);
                var matches = avatarParameters
                    .Where(parameterName => parameterName.StartsWith(wildcardPrefix, StringComparison.OrdinalIgnoreCase))
                    .Where(parameterName => !IsExcluded(parameterName))
                    .ToList();

                if (matches.Count == 0)
                {
                    unmatchedWildcards.Add(entry);
                    continue;
                }

                foreach (var match in matches)
                {
                    if (seen.Add(match))
                        resolved.Add(match);
                }
            }

            return resolved;
        }

        private void AddToVRCParametersList(VRCExpressionParameters.Parameter parameter)
        {
            List<VRCExpressionParameters.Parameter> newParamsList =
                new List<VRCExpressionParameters.Parameter>();

            foreach (var param in _configuration.VRCParameters.parameters)
            {
                newParamsList.Add(param);
            }

            newParamsList.Add(parameter);
            _configuration.VRCParameters.parameters = newParamsList.ToArray();
        }

        private void AddBoolParameter(string parameter, bool defaultBool, bool saved = true)
        {
            _configuration.Controller.AddParameter(new AnimatorControllerParameter
            {
                type = AnimatorControllerParameterType.Bool,
                name = parameter,
                defaultBool = defaultBool,
            });

            AddToVRCParametersList(new VRCExpressionParameters.Parameter
            {
                name = parameter,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = defaultBool ? 1.0f : 0.0f,
                saved = saved,
                networkSynced = false
            });
        }

        private void AddFloatParameter(string parameter, float defaultFloat, bool saved = true)
        {
            _configuration.Controller.AddParameter(new AnimatorControllerParameter
            {
                type = AnimatorControllerParameterType.Float,
                name = parameter,
                defaultFloat = defaultFloat,
            });

            AddToVRCParametersList(new VRCExpressionParameters.Parameter
            {
                name = parameter,
                valueType = VRCExpressionParameters.ValueType.Float,
                defaultValue = defaultFloat,
                saved = saved,
                networkSynced = false
            });
        }

        private void AddIntParameter(string parameter, int defaultInt, bool saved = true)
        {
            _configuration.Controller.AddParameter(new AnimatorControllerParameter
            {
                type = AnimatorControllerParameterType.Int,
                name = parameter,
                defaultInt = defaultInt,
            });

            AddToVRCParametersList(new VRCExpressionParameters.Parameter
            {
                name = parameter,
                valueType = VRCExpressionParameters.ValueType.Int,
                defaultValue = defaultInt,
                saved = saved,
                networkSynced = false
            });
        }

        private void CreateSavablePreset(SavablePreset preset, ref AnimatorControllerLayer animatorLayer,
            ref AnimatorState idleState)
        {
            string baseParameter = JoinParameterPath("SA", preset.Name);
            var resolvedParameters = ResolvePresetParameters(preset, out var unmatchedWildcards);

            if (unmatchedWildcards.Count > 0)
            {
                Debug.LogWarning(
                    $"Savable Preset '{preset.Name}' has wildcard entries that matched no avatar parameters: {string.Join(", ", unmatchedWildcards)}",
                    _configuration);
            }

            // Helper parameters
            AddBoolParameter(JoinParameterPath(baseParameter, "Load"), false, false);
            AddBoolParameter(JoinParameterPath(baseParameter, "Save"), false, false);
            AddBoolParameter(JoinParameterPath(baseParameter, "Reset"), false, false);
            AddBoolParameter(JoinParameterPath(baseParameter, "Has Saved"), false, true);

            // Create States
            var loadState = animatorLayer.stateMachine.AddState($"{preset.Name} - Load");
            var loadNotSavedState = animatorLayer.stateMachine.AddState($"{preset.Name} - Load (Not Saved)");
            var saveState = animatorLayer.stateMachine.AddState($"{preset.Name} - Save");
            var resetState = animatorLayer.stateMachine.AddState($"{preset.Name} - Reset");
            CreateTransitions(ref idleState, ref loadState, ref loadNotSavedState, ref saveState, ref resetState,
                baseParameter);

            // Add All Parameters
            AddParameters(resolvedParameters, baseParameter);

            // Add Behaviors to states
            AddLoadBehavior(loadState, resolvedParameters, baseParameter);
            AddLoadNotSavedBehavior(loadNotSavedState, baseParameter);
            AddSaveBehavior(saveState, resolvedParameters, baseParameter);
            AddResetBehavior(resetState, resolvedParameters, baseParameter);
        }

        private void CreateTransitions(ref AnimatorState idleState, ref AnimatorState loadState,
            ref AnimatorState loadNotSavedState, ref AnimatorState saveState, ref AnimatorState resetState,
            string baseParameter)
        {
            // Load
            idleState.AddTransition(Register(new AnimatorStateTransition
            {
                duration = 0.0f,
                hasExitTime = false,
                exitTime = 0.0f,
                hasFixedDuration = true,
                destinationState = loadState,
                conditions = new[]
                {
                    new AnimatorCondition
                    {
                        parameter = JoinParameterPath(baseParameter, "Load"),
                        mode = AnimatorConditionMode.If,
                        threshold = 1.0f
                    },
                    new AnimatorCondition
                    {
                        parameter = JoinParameterPath(baseParameter, "Has Saved"),
                        mode = AnimatorConditionMode.If,
                        threshold = 1.0f
                    }
                }
            }));
            var loadExistTransition = loadState.AddExitTransition(true);
            SetExitTransitionSettings(ref loadExistTransition, baseParameter, "Load");

            // Load (Not Saved)
            idleState.AddTransition(Register(new AnimatorStateTransition
            {
                duration = 0.0f,
                hasExitTime = false,
                exitTime = 0.0f,
                hasFixedDuration = true,
                destinationState = loadNotSavedState,
                conditions = new[]
                {
                    new AnimatorCondition
                    {
                        parameter = JoinParameterPath(baseParameter, "Load"),
                        mode = AnimatorConditionMode.If,
                        threshold = 1.0f
                    },
                    new AnimatorCondition
                    {
                        parameter = JoinParameterPath(baseParameter, "Has Saved"),
                        mode = AnimatorConditionMode.IfNot,
                        threshold = 1.0f
                    }
                }
            }));
            var loadNotSavedExistTransition = loadNotSavedState.AddExitTransition(true);
            SetExitTransitionSettings(ref loadNotSavedExistTransition, baseParameter, "Load");

            // Save
            idleState.AddTransition(Register(new AnimatorStateTransition
            {
                duration = 0.0f,
                hasExitTime = false,
                exitTime = 0.0f,
                hasFixedDuration = true,
                destinationState = saveState,
                conditions = new[]
                {
                    new AnimatorCondition
                    {
                        parameter = JoinParameterPath(baseParameter, "Save"),
                        mode = AnimatorConditionMode.If,
                        threshold = 1.0f
                    }
                }
            }));
            var saveExitTransition = saveState.AddExitTransition(true);
            SetExitTransitionSettings(ref saveExitTransition, baseParameter, "Save");

            // Reset
            idleState.AddTransition(Register(new AnimatorStateTransition
            {
                duration = 0.0f,
                hasExitTime = false,
                exitTime = 0.0f,
                hasFixedDuration = true,
                destinationState = resetState,
                conditions = new[]
                {
                    new AnimatorCondition
                    {
                        parameter = JoinParameterPath(baseParameter, "Reset"),
                        mode = AnimatorConditionMode.If,
                        threshold = 1.0f
                    }
                }
            }));
            var resetExitTransition = resetState.AddExitTransition(true);
            SetExitTransitionSettings(ref resetExitTransition, baseParameter, "Reset");
        }

        private AnimatorStateTransition Register(AnimatorStateTransition transition)
        {
            AssetDatabase.AddObjectToAsset(transition, _configuration.Controller);

            transition.hideFlags = HideFlags.HideInHierarchy;
            if (transition.destinationState) transition.name = $"Transition -> {transition.destinationState.name}";

            return transition;
        }

        private void SetExitTransitionSettings(ref AnimatorStateTransition transition, string baseParameter,
            string parameter)
        {
            transition.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.IfNot,
                    parameter = JoinParameterPath(baseParameter, parameter),
                }
            };
            transition.exitTime = 0f;
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.offset = 0f;
            transition.interruptionSource = TransitionInterruptionSource.None;
        }

        private void AddParameters(IEnumerable<string> parameters, string baseParameter)
        {
            foreach (var parameter in parameters)
            {
                string parameterName = JoinParameterPath(baseParameter, parameter);
                AddFloatParameter(parameterName, 0.0f);
            }
        }

        private void AddLoadBehavior(AnimatorState loadState, IEnumerable<string> parameters, string baseParameter)
        {
            var parameterDriver = loadState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            parameterDriver.localOnly = true;

            foreach (var parameter in parameters)
            {
                parameterDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Copy,
                    source = JoinParameterPath(baseParameter, parameter),
                    name = parameter, // Destination
                });
            }

            AddToParameterDriverLast(ref parameterDriver, baseParameter, "Load", 0.0f);
        }

        private void AddLoadNotSavedBehavior(AnimatorState loadNotSavedState, string baseParameter)
        {
            var parameterDriver = loadNotSavedState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            parameterDriver.localOnly = true;

            AddToParameterDriverLast(ref parameterDriver, baseParameter, "Load", 0.0f);
        }

        private void AddSaveBehavior(AnimatorState saveState, IEnumerable<string> parameters, string baseParameter)
        {
            var parameterDriver = saveState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            parameterDriver.localOnly = true;

            foreach (var parameter in parameters)
            {
                parameterDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Copy,
                    source = parameter,
                    name = JoinParameterPath(baseParameter, parameter), // Destination
                });
            }

            AddToParameterDriverLast(ref parameterDriver, baseParameter, "Has Saved", 1.0f);
            AddToParameterDriverLast(ref parameterDriver, baseParameter, "Save", 0.0f);
        }

        private void AddResetBehavior(AnimatorState resetState, IEnumerable<string> parameters, string baseParameter)
        {
            var parameterDriver = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            parameterDriver.localOnly = true;

            foreach (var parameter in parameters)
            {
                parameterDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = JoinParameterPath(baseParameter, parameter),
                    value = 0.0f,
                });
            }

            AddToParameterDriverLast(ref parameterDriver, baseParameter, "Has Saved", 0.0f);
            AddToParameterDriverLast(ref parameterDriver, baseParameter, "Reset", 0.0f);
        }

        private void AddToParameterDriverLast(ref VRCAvatarParameterDriver parameterDriver, string baseParameter,
            string parameter, float value)
        {
            parameterDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = JoinParameterPath(baseParameter, parameter),
                value = value,
            });
        }

        #endregion
    }
}