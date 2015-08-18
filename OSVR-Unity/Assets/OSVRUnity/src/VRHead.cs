/// OSVR-Unity Connection
///
/// http://sensics.com/osvr
///
/// <copyright>
/// Copyright 2014 Sensics, Inc.
///
/// Licensed under the Apache License, Version 2.0 (the "License");
/// you may not use this file except in compliance with the License.
/// You may obtain a copy of the License at
///
///     http://www.apache.org/licenses/LICENSE-2.0
///
/// Unless required by applicable law or agreed to in writing, software
/// distributed under the License is distributed on an "AS IS" BASIS,
/// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
/// See the License for the specific language governing permissions and
/// limitations under the License.
/// </copyright>
/// <summary>
/// Author: Bob Berkebile
/// Email: bob@bullyentertainment.com || bobb@pixelplacement.com
/// </summary>

using UnityEngine;
using System.Collections;

namespace OSVR
{
    namespace Unity
    {

        public enum ViewMode { stereo, mono };

        [RequireComponent(typeof(Camera))]
        [RequireComponent(typeof(DisplayInterface))]
        public class VRHead : MonoBehaviour
        {
            #region Public Variables
            public ViewMode viewMode;

            [Range(0, 1)]
            public float stereoAmount;
            public float maxStereo = .03f;
            public Camera Camera { get { return _camera; } set { _camera = value; } }
            #endregion

            #region Private Variables
            private ClientKit _clientKit;
            private VREye _leftEye;
            private VREye _rightEye;
            private bool swapEyes = false;
            private float _previousStereoAmount;
            private ViewMode _previousViewMode;
            private Camera _camera;
            private DeviceDescriptor _deviceDescriptor;
            private DisplayInterface _displayInterface;
            private bool _initDisplayInterface = false;
            private bool renderedStereo = true;
            #endregion

            #region Init
            void Awake()
            {
                _clientKit = FindObjectOfType<ClientKit>();
            }
            void Start()
            {
                Init();
                CatalogEyes();
/*
                if (_distortionEffect != null)
                {
                    _distortionEffect.enabled = (viewMode == ViewMode.mono);
                }
*/
                _displayInterface = GetComponent<DisplayInterface>();
                _displayInterface.DisplayConfig = _clientKit.context.GetDisplayConfig();

                //update VRHead with info from the display interface if it has been initialized
                //it might not be initialized if it is still parsing a display json file
                //in that case, we will try to initialize asap in the update function
                if (_displayInterface != null && _displayInterface.Initialized)
                {
                    UpdateDisplayInterface();                  
                }
                //else initialize in Update() 
            }
            void OnEnable()
            {
                StartCoroutine("EndOfFrame");
            }

            void OnDisable()
            {
                StopCoroutine("EndOfFrame");
            }
            #endregion

            #region Loop
            void Update()
            {

                if (!_initDisplayInterface && !_displayInterface.Initialized)
                {
                    //if the display configuration hasn't initialized, ping the DisplayInterface to retrieve it from the ClientKit
                    //this would mean DisplayInterface was unable to retrieve that data in its Start() function.
                    _displayInterface.ReadDisplayPath();
                }
                else if (!_initDisplayInterface && _displayInterface.Initialized)
                {
                    //once the DisplayInterface has initialized, meaning it has read data from the /display path which contains
                    //display configuration, update the Camera and other settings with properties from the display configuration.
                    UpdateDisplayInterface();
                }
                else if (_initDisplayInterface)
                {
                    UpdateStereoAmount();
                    UpdateViewMode();
                }
            }
            #endregion

            #region Public Methods
            #endregion

            #region Private Methods
            private void UpdateDisplayInterface()
            {
                GetDeviceDescription();
                MatchEyes(); //copy camera properties to each eye
                //rotate each eye based on overlap percent, must do this after match eyes
                if (_deviceDescriptor != null)
                {
                    SetEyeRotation(_deviceDescriptor.OverlapPercent, _deviceDescriptor.MonocularHorizontal);
                    SetEyeRoll(_deviceDescriptor.LeftRoll, _deviceDescriptor.RightRoll);
                }
                _initDisplayInterface = true;
            }
            void UpdateViewMode()
            {
                if (Time.realtimeSinceStartup < 100 || _previousViewMode != viewMode)
                {
                    switch (viewMode)
                    {
                        case ViewMode.mono:
                            Camera.enabled = true;
                            _leftEye.Camera.enabled = false;
                            _rightEye.Camera.enabled = false;
                            break;

                        case ViewMode.stereo:
                            Camera.enabled = false;
                            _leftEye.Camera.enabled = true;
                            _rightEye.Camera.enabled = true;
                            
                            break;
                    }
                }

                _previousViewMode = viewMode;
            }

            void UpdateStereoAmount()
            {
                if (stereoAmount != _previousStereoAmount)
                {
                    stereoAmount = Mathf.Clamp(stereoAmount, 0, 1);
                    _rightEye.cachedTransform.localPosition = (swapEyes ? Vector3.left : Vector3.right) * (maxStereo * stereoAmount);
                    _leftEye.cachedTransform.localPosition = (swapEyes ? Vector3.right : Vector3.left) * (maxStereo * stereoAmount);
                    _previousStereoAmount = stereoAmount;                  
                }
            }

            //this function finds and initializes each eye
            void CatalogEyes()
            {
                foreach (VREye currentEye in GetComponentsInChildren<VREye>())
                {
                    //catalog:
                    switch (currentEye.eye)
                    {
                        case Eye.left:
                            _leftEye = currentEye;
                            break;

                        case Eye.right:
                            _rightEye = currentEye;
                            break;
                    }
                }
            }

            //this function matches the camera on each eye to the camera on the head
            void MatchEyes()
            {
                foreach (VREye currentEye in GetComponentsInChildren<VREye>())
                {
                    //match:
                    currentEye.MatchCamera(Camera);
                }
            }

            void Init()
            {
                if (Camera == null)
                {
                    if ((Camera = GetComponent<Camera>()) == null)
                    {
                        Camera = gameObject.AddComponent<Camera>();
                    }               
                }

                //VR should never timeout the screen:
                Screen.sleepTimeout = SleepTimeout.NeverSleep;

                //60 FPS whenever possible:
                Application.targetFrameRate = 60;

                _initDisplayInterface = false;
            }

            /// <summary>
            /// GetDeviceDescription: Get a Description of the HMD and apply appropriate settings
            /// 
            /// </summary>
            private void GetDeviceDescription()
            {
                _deviceDescriptor = _displayInterface.GetDeviceDescription();
                if (_deviceDescriptor != null)
                {
                    switch (_deviceDescriptor.DisplayMode)
                    {
                        case "full_screen":
                            viewMode = ViewMode.mono;
                            break;
                        case "horz_side_by_side":
                        case "vert_side_by_side":
                        default:
                            viewMode = ViewMode.stereo;
                            break;
                    }
                    //@todo get these from DisplayConfig?
                    swapEyes = _deviceDescriptor.SwapEyes > 0; //swap left and right eye positions?
                    stereoAmount = Mathf.Clamp(_deviceDescriptor.OverlapPercent, 0, 100);
                    
                    SetResolution(_deviceDescriptor.Width, _deviceDescriptor.Height); //set resolution before FOV
                    //@todo get resolution from display config or rendermanager?
                    
                    Camera.fieldOfView = Mathf.Clamp(_deviceDescriptor.MonocularVertical, 0, 180); //unity camera FOV is vertical
                    //@todo get the field of view from DisplayConfig?
                    //Camera.fieldOfView = _displayInterface.GetFieldOfView();

                    //@todo get aspect ratio (resolution) from DisplayConfig
                    float aspectRatio = (float)_deviceDescriptor.Width / (float)_deviceDescriptor.Height;
                    //aspect ratio per eye depends on how many displays the HMD has
                    //for example, dSight has two 1920x1080 displays, so each eye should have 1.77 aspect
                    //whereas HDK has one 1920x1080 display, each eye should have 0.88 aspect (half of 1.77)
                    float aspectRatioPerEye = _deviceDescriptor.NumDisplays == 1 ? aspectRatio * 0.5f : aspectRatio;
                    
                    //set projection matrix for each eye
                    Camera.projectionMatrix = Matrix4x4.Perspective(_deviceDescriptor.MonocularVertical, aspectRatioPerEye, Camera.nearClipPlane, Camera.farClipPlane);
                    //Camera.projectionMatrix = _displayInterface.GetProjectionMatrix(_leftEye);
                    
                    //@todo get these values from RenderManager?
                    SetDistortion(_deviceDescriptor.K1Red, _deviceDescriptor.K1Green, _deviceDescriptor.K1Blue, 
                    _deviceDescriptor.CenterProjX, _deviceDescriptor.CenterProjY); //set distortion shader
            
                    //if the view needs to be rotated 180 degrees, create a parent game object that is flipped 180 degrees on the z axis.
                    if (_deviceDescriptor.Rotate180 > 0)
                    {
                        GameObject vrHeadParent = new GameObject();
                        vrHeadParent.name = this.transform.name + "_parent";
                        vrHeadParent.transform.position = this.transform.position;
                        vrHeadParent.transform.rotation = this.transform.rotation;
                        if (this.transform.parent != null)
                        {
                            vrHeadParent.transform.parent = this.transform.parent;
                        }
                        this.transform.parent = vrHeadParent.transform;
                        vrHeadParent.transform.Rotate(0, 0, 180, Space.Self);
                    }
                }
            }

            private void SetDistortion(float k1Red, float k1Green, float k1Blue, float centerProjX, float centerProjY)
            {
              SetDistortion(_leftEye, k1Red, k1Green, k1Blue, new Vector2(centerProjX, centerProjY));
              SetDistortion(_rightEye, k1Red, k1Green, k1Blue, new Vector2(centerProjX, centerProjY));
            }
            private void SetDistortion(VREye eye, float k1Red, float k1Green, float k1Blue, Vector2 center)
            {
              // disable distortion if there is no distortion for this HMD
              if (k1Red == 0 && k1Green == 0 && k1Blue == 0)
              {
                if (eye.DistortionEffect)
                {
                  eye.DistortionEffect.enabled = false;
                }
                return;
              }
              // Otherwise try to create distortion and set its parameters
              var distortionFactory = new K1RadialDistortionFactory();
              var effect = distortionFactory.GetOrCreateDistortion(eye);
              if (effect)
              {
                effect.k1Red = k1Red;
                effect.k1Green = k1Green;
                effect.k1Blue = k1Blue;
                effect.center = center;
              }
            }

            //Set the Screen Resolution
            private void SetResolution(int width, int height)
            {
                //set the resolution, default to full screen
                Screen.SetResolution(width, height, true);
#if UNITY_EDITOR
                UnityEditor.PlayerSettings.defaultScreenWidth = width;
                UnityEditor.PlayerSettings.defaultScreenHeight = height;
                UnityEditor.PlayerSettings.defaultIsFullScreen = true;
#endif
            }
            
            //rotate each eye based on overlap percent and horizontal FOV
            //Formula: ((OverlapPercent/100) * hFOV)/2
            private void SetEyeRotation(float overlapPercent, float horizontalFov)
            {
                float overlap = overlapPercent* .01f * horizontalFov * 0.5f;
                
                //with a 90 degree FOV with 100% overlap, the eyes should not be rotated
                //compare rotationY with half of FOV

                float halfFOV = horizontalFov * 0.5f;
                float rotateYAmount = Mathf.Abs(overlap - halfFOV);

                foreach (VREye currentEye in GetComponentsInChildren<VREye>())
                {
                    switch (currentEye.eye)
                    {
                        case Eye.left:
                            _leftEye.SetEyeRotationY(-rotateYAmount);
                            break;
                        case Eye.right:
                            _rightEye.SetEyeRotationY(rotateYAmount);
                            break;
                    }
                }
            }
            //rotate each eye on the z axis by the specified amount, in degrees
            private void SetEyeRoll(float leftRoll, float rightRoll)
            {
                foreach (VREye currentEye in GetComponentsInChildren<VREye>())
                {
                    switch (currentEye.eye)
                    {
                        case Eye.left:
                            _leftEye.SetEyeRoll(leftRoll);
                            break;
                        case Eye.right:
                            _rightEye.SetEyeRoll(rightRoll);
                            break;
                    }
                }
            }

            void OnPreCull()
            {
                if (viewMode == ViewMode.mono)
                {
                    // Nothing to do.
                    return;
                }

                //@todo update the client?

                //@todo update the head tracker?

                // Turn off the mono camera so it doesn't waste time rendering.
                // @note mono camera is left on from beginning of frame till now
                // in order that other game logic (e.g. Camera.main) continues
                // to work as expected.
                _camera.enabled = false;

                // Render the eyes under our control.
                _leftEye.Render(_displayInterface);
                _rightEye.Render(_displayInterface);

                // Remember to reenable.
                renderedStereo = true;
            }

            IEnumerator EndOfFrame()
            {
                while (true)
                {
                    // If *we* turned off the mono cam, turn it back on for next frame.
                    if (renderedStereo)
                    {
                        _camera.enabled = true;
                        renderedStereo = false;
                    }
                    yield return new WaitForEndOfFrame();
                    //use this when RenderManager is around
                   /* if (SupportsRenderManager())
                    {
                        //@todo update tracker state?
                        //call the rendering plugin
                        GL.IssuePluginEvent(0);
                        //This invalidates any cached renderstates tied to the GL context. 
                        //If a (native) plugin alters the renderstate settings then Unity's 
                        //rendering architecture must be made aware of that to not assume the GL context is preserved.
                        GL.InvalidateState();
                    }*/
                }
            }
            #endregion
        }
    }
}
