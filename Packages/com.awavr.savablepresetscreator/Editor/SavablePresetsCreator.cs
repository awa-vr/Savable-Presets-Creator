using System;
using System.Collections.Generic;
using System.Linq;
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

        // private List<VRCAvatarDescriptor> _avatars;
        // private VRCAvatarDescriptor _avatar;
        // private AnimatorController _fx;
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
            // _avatars = Core.GetAvatarsInScene();
            //
            // if (_avatars.Count == 0)
            // {
            //     EditorGUILayout.HelpBox("Please place an avatar in the scene", MessageType.Error);
            //     _avatars = null;
            //     return;
            // }
            //
            // if (_avatars.Count == 1)
            // {
            //     _avatar = _avatars.First();
            //     _avatars.Clear();
            //     return;
            // }
        }

        public void OnGUI()
        {
            Core.Title(_windowTitle);

            // if (!GetAvatar())
            //     return;
            //
            // if (!GetFXController())
            //     return;

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
        }

        #endregion

        #region GUIHelpers

        // private bool GetAvatar()
        // {
        //     Core.GetAvatar(ref _avatar, ref _avatars);
        //     if (!_avatar)
        //     {
        //         EditorGUILayout.HelpBox("Please select an avatar", MessageType.Error);
        //         return false;
        //     }
        //
        //     return true;
        // }
        //
        // private bool GetFXController()
        // {
        //     _fx = Core.GetAnimatorController(_avatar);
        //
        //     if (_fx == null)
        //     {
        //         EditorGUILayout.HelpBox("Can't find an FX animator on your avatar. Please add one", MessageType.Error);
        //         return false;
        //     }
        //
        //     return true;
        // }

        private void DrawConfigurationField()
        {
            _configuration = EditorGUILayout.ObjectField(
                    "Configuration",
                    _configuration,
                    typeof(SavablePresetConfiguration),
                    false)
                as SavablePresetConfiguration;
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
            }
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

        private void AddBoolParameter(string parameter, bool defaultBool)
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
                saved = true,
                networkSynced = false
            });
        }

        private void AddFloatParameter(string parameter, float defaultFloat)
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
                saved = true,
                networkSynced = false
            });
        }

        private void AddIntParameter(string parameter, int defaultInt)
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
                saved = true,
                networkSynced = false
            });
        }

        private void CreateSavablePreset(SavablePreset preset, ref AnimatorControllerLayer animatorLayer,
            ref AnimatorState idleState)
        {
            string baseParameter = JoinParameterPath("SA", preset.Name);

            // Helper parameters
            AddBoolParameter(JoinParameterPath(baseParameter, "Load"), false);
            AddBoolParameter(JoinParameterPath(baseParameter, "Save"), false);
            AddBoolParameter(JoinParameterPath(baseParameter, "Reset"), false);
            AddBoolParameter(JoinParameterPath(baseParameter, "Has Saved"), false);

            // Create States
            var loadState = animatorLayer.stateMachine.AddState($"{preset.Name} - Load");
            var loadNotSavedState = animatorLayer.stateMachine.AddState($"{preset.Name} - Load (Not Saved)");
            var saveState = animatorLayer.stateMachine.AddState($"{preset.Name} - Save");
            var resetState = animatorLayer.stateMachine.AddState($"{preset.Name} - Reset");
            CreateTransitions(ref idleState, ref loadState, ref loadNotSavedState, ref saveState, ref resetState,
                baseParameter);

            // Add All Parameters
            AddParameters(preset, baseParameter);

            // Add Behaviors to states
            AddLoadBehavior(loadState, preset, baseParameter);
            AddLoadNotSavedBehavior(loadNotSavedState, preset, baseParameter);
            AddSaveBehavior(saveState, preset, baseParameter);
            AddResetBehavior(resetState, preset, baseParameter);
        }

        private void CreateTransitions(ref AnimatorState idleState, ref AnimatorState loadState,
            ref AnimatorState loadNotSavedState, ref AnimatorState saveState, ref AnimatorState resetState,
            string baseParameter)
        {
            // Load
            idleState.AddTransition(new AnimatorStateTransition
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
            });
            var loadExistTransition = loadState.AddExitTransition(true);
            SetExitTransitionSettings(ref loadExistTransition, baseParameter, "Load");

            // Load (Not Saved)
            idleState.AddTransition(new AnimatorStateTransition
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
            });
            var loadNotSavedExistTransition = loadNotSavedState.AddExitTransition(true);
            SetExitTransitionSettings(ref loadNotSavedExistTransition, baseParameter, "Load");

            // Save
            idleState.AddTransition(new AnimatorStateTransition
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
            });
            var saveExitTransition = saveState.AddExitTransition(true);
            SetExitTransitionSettings(ref saveExitTransition, baseParameter, "Save");

            // Reset
            idleState.AddTransition(new AnimatorStateTransition
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
            });
            var resetExitTransition = resetState.AddExitTransition(true);
            SetExitTransitionSettings(ref resetExitTransition, baseParameter, "Reset");
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

        private void AddParameters(SavablePreset preset, string baseParameter)
        {
            foreach (var parameter in preset.Parameters)
            {
                string parameterName = JoinParameterPath(baseParameter, parameter);
                AddFloatParameter(parameterName, 0.0f);
            }
        }

        private void AddLoadBehavior(AnimatorState loadState, SavablePreset preset, string baseParameter)
        {
            var parameterDriver = loadState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            parameterDriver.localOnly = true;

            foreach (var parameter in preset.Parameters)
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

        private void AddLoadNotSavedBehavior(AnimatorState loadNotSavedState, SavablePreset preset,
            string baseParameter)
        {
            var parameterDriver = loadNotSavedState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            parameterDriver.localOnly = true;

            AddToParameterDriverLast(ref parameterDriver, baseParameter, "Load", 0.0f);
        }

        private void AddSaveBehavior(AnimatorState saveState, SavablePreset preset, string baseParameter)
        {
            var parameterDriver = saveState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            parameterDriver.localOnly = true;

            foreach (var parameter in preset.Parameters)
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

        private void AddResetBehavior(AnimatorState resetState, SavablePreset preset, string baseParameter)
        {
            var parameterDriver = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            parameterDriver.localOnly = true;

            foreach (var parameter in preset.Parameters)
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