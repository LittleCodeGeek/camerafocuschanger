//#define debugCFC

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using KSP.IO;


namespace CameraFocusChanger
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CameraFocusChanger : MonoBehaviour
    {
        FlightCamera flightCamera;
        float pivotTranslateSharpness;
        Transform targetTransform;
        UnityEngine.KeyCode actionKey = KeyCode.O;
        float startFocusTime;
        bool hasReachedTarget;
        bool isFocusing;
        bool showUpdateMessage = true;


        void DebugPrint(string text)
        {
#if debugCFC
            print("[CFC] " + text);
#endif
        }

        void Start()
        {
            DebugPrint("Starting Camera Focus Changer");
            flightCamera = FlightCamera.fetch;
            pivotTranslateSharpness = 0.5f;
            hasReachedTarget = false;
            isFocusing = false;

            PluginConfiguration config = PluginConfiguration.CreateForType<CameraFocusChanger>();
            config.load();
            actionKey = config.GetValue<KeyCode>("actionKey", KeyCode.O);
            showUpdateMessage = config.GetValue<bool>("showUpdateMessage", true);

            GameEvents.OnCameraChange.Add(OnCameraChange);
            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroy);
            GameEvents.onVesselGoOnRails.Add(OnVesselGoOnRails);
            GameEvents.onStageSeparation.Add(OnStageSeparation);
            GameEvents.onUndock.Add(OnUndock);

            API.SetInstance(this);
        }

        void OnDestroy()
        {
            DebugPrint("Disabled");

            GameEvents.OnCameraChange.Remove(OnCameraChange);
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroy);
            GameEvents.onVesselGoOnRails.Remove(OnVesselGoOnRails);
            GameEvents.onStageSeparation.Remove(OnStageSeparation);
            GameEvents.onUndock.Remove(OnUndock);

            API.SetInstance(null);
        }

        void OnCameraChange(CameraManager.CameraMode cameraMode)
        {
            DebugPrint(string.Format("camera mode changed to {0}", cameraMode.ToString()));
            if (cameraMode == CameraManager.CameraMode.IVA && targetTransform != null)
            {
                ResetFocus();
            }
        }

        void OnVesselChange(Vessel vessel)
        {
            DebugPrint(string.Format("vessel changed to {0}", vessel.vesselName));
            CheckForVesselChanged();
        }

        void OnVesselWillDestroy(Vessel vessel)
        {
            if (targetTransform != null)
            {
                Part part = Part.FromGO(targetTransform.gameObject);
                if (part != null && part.vessel == vessel)
                {
                    DebugPrint("vessel about to be destroyed");
                    ResetFocus();
                }
            }
        }

        void OnVesselGoOnRails(Vessel vessel)
        {
            if (targetTransform != null)
            {
                Part part = Part.FromGO(targetTransform.gameObject);
                if (part != null && part.vessel == vessel)
                {
                    DebugPrint("vessel about to be packed");
                    ResetFocus();
                }
            }
        }

        void OnStageSeparation(EventReport report)
        {
            CheckForVesselChanged();
        }

        void OnUndock(EventReport report)
        {
            CheckForVesselChanged();
        }

        void CheckForVesselChanged()
        {
            Vessel currentVessel = FlightGlobals.ActiveVessel;
            if (targetTransform != null)
            {
                Part part = Part.FromGO(targetTransform.gameObject);
                if (part != null && part.vessel != currentVessel)
                {
                    DebugPrint("vessel missmatch");
                    string message = string.Format("CFC WARNING\n!!!Controlled Vessel is not Focussed!!!");
                    var screenMessage = new ScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
                    ScreenMessages.PostScreenMessage(screenMessage);
                }
            }
        }

        void Update()
        {
            // check if we are trying to change the focus
            GameObject obj = EventSystem.current.currentSelectedGameObject;
            bool inputFieldIsFocused = InputLockManager.IsLocked(ControlTypes.ALL_SHIP_CONTROLS) || (obj != null && obj.GetComponent<InputField>() != null && obj.GetComponent<InputField>().isFocused);
            if (!inputFieldIsFocused && Input.GetKeyDown(actionKey))
            {
                DebugPrint("updating camera focus");

                if ((Time.time - startFocusTime) < 0.25f)
                {
                    hasReachedTarget = true;
                }
                else
                {
                    // find a part under the mouse, if there is one, set it as the camera's point of focus
                    // otherwise, revert to the center of mass
                    Transform raycastTransform = GetTransform();
                    if (raycastTransform != null)
                    {
                        FocusOn(raycastTransform);
                    }
                    else if (targetTransform != null)
                    {
                        ResetFocus();
                    }
                    else
                    {
                        hasReachedTarget = true;
                    }
                }
            }

            if (!inputFieldIsFocused && Input.GetKeyDown(KeyCode.Y))
            {
                DebugPrint(string.Format("target: {0}", targetTransform));
                DebugPrint(string.Format("vessel: {0}", FlightGlobals.ActiveVessel.GetWorldPos3D()));
                DebugPrint(string.Format("camera: {0}", flightCamera.transform.parent.position));
            }

            UpdateFocus();
        }

        public void FocusOn(Transform transform)
        {
            DebugPrint(string.Format("tound target {0}", transform.gameObject.name));
            if (flightCamera.pivotTranslateSharpness > 0)
            {
                pivotTranslateSharpness = flightCamera.pivotTranslateSharpness;
                DebugPrint(string.Format("sharpness of {0}", pivotTranslateSharpness));
            }
            flightCamera.pivotTranslateSharpness = 0;

            // targeting the same part twice will make the camera jump to it
            hasReachedTarget = transform == targetTransform;

            if (showUpdateMessage)
            {
                Part part = Part.FromGO(transform.gameObject);
                string message = string.Format("CFC Actived ({0})", part ? part.partInfo.title : "." + transform.gameObject.name);
                var screenMessage = new ScreenMessage(message, 1.5f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(screenMessage);
            }

            startFocusTime = Time.time;
            isFocusing = true;
            targetTransform = transform;
        }

        public void ResetFocus()
        {
            DebugPrint("Reset Target");
            if (showUpdateMessage)
            {
                var screenMessage = new ScreenMessage("CFC Deactivated", 1.5f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(screenMessage);
            }

            targetTransform = null;
            hasReachedTarget = false;
            isFocusing = true;
            startFocusTime = Time.time;
            flightCamera.pivotTranslateSharpness = pivotTranslateSharpness;
        }

        void UpdateFocus()
        {
            // do we have a target for the camera focus
            if (isFocusing)
            {
                Vessel vessel = FlightGlobals.ActiveVessel;
                Vector3d currentPosition = vessel.GetWorldPos3D();

                Vector3 targetPosition = targetTransform != null ? targetTransform.position : new Vector3((float)currentPosition.x, (float)currentPosition.y, (float)currentPosition.z);

                Vector3 positionDifference = flightCamera.transform.parent.position - targetPosition;
                float distance = positionDifference.magnitude;

                //if (distance >= 0.015f)
                    //print(string.Format("Distance of {0}", distance));

                if (hasReachedTarget || distance < 0.015f)
                {
                    flightCamera.transform.parent.position = targetPosition;
                    hasReachedTarget = true;
                    isFocusing = targetTransform != null;
                }
                else
                {
                    //DebugPrint(string.Format("Moving by {0}", (positionDifference.normalized * Time.fixedDeltaTime * (distance * Math.Max(4 - distance, 1))).magnitude));
                    flightCamera.transform.parent.position -= positionDifference.normalized * Time.fixedDeltaTime * (distance * Math.Max(4 - distance, 1));
                    // if the parts are not of the same craft, boost the speed at which we move towards it
                    Part part = targetTransform != null ? Part.FromGO(targetTransform.gameObject) : null;
                    if ((part != null && part.vessel != vessel) || targetTransform == null)
                    {
                        flightCamera.transform.parent.position -= positionDifference.normalized * Time.fixedDeltaTime;
                        if (Time.time - startFocusTime > 10.0f)
                            hasReachedTarget = true;
                    }
                }
            }
        }

        Transform GetTransform()
        {
            Vector3 aim = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0);
            Ray ray = flightCamera.mainCamera.ScreenPointToRay(aim);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000, 0x80001))
            {
                return hit.transform;
            }
            return null;
        }
    }

    static public class API
    {
        static private CameraFocusChanger s_cfcInstance = null;

        static public void SetInstance(CameraFocusChanger cfcInstance)
        {
            s_cfcInstance = cfcInstance;
        }

        static public bool IsCFCAvailable()
        {
            return s_cfcInstance != null;
        }

        static public bool FocusOnPart(Part part)
        {
            if (s_cfcInstance != null)
            {
                if (part != null)
                {
                    s_cfcInstance.FocusOn(part.transform);
                }
                else
                {
                    s_cfcInstance.ResetFocus();
                }
                return true;
            }

            return false;
        }

        static public bool ResetFocus()
        {
            if (s_cfcInstance != null)
            {
                s_cfcInstance.ResetFocus();
                return true;
            }

            return false;
        }
    }
}
