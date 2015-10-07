using System;
using UnityEngine;
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
        Vector3d previousPosition;
        bool hasReachedTarget;
        bool isFocusing;

        void Start()
        {
            //print("Starting Camera Focus Changer");
            flightCamera = FlightCamera.fetch;
            pivotTranslateSharpness = 0.5f;
            hasReachedTarget = false;
            isFocusing = false;

            PluginConfiguration config = PluginConfiguration.CreateForType<CameraFocusChanger>();
            config.load();
            actionKey = config.GetValue<KeyCode>("actionKey");
        }

        void Update()
        {
            // check if we are trying to change the focus
            if (Input.GetKeyDown(actionKey))
            {
                //print("Updating Camera Focus");

                // find a part under the mouse, if there is one, set it as the camera's point of focus
                // otherwise, revert to the center of mass
                Transform raycastTransform = GetTransform();
                if (raycastTransform != null)
                {
                    //print(string.Format("Found Target {0}", raycastTransform.gameObject.name));
                    if (flightCamera.pivotTranslateSharpness > 0)
                    {
                        pivotTranslateSharpness = flightCamera.pivotTranslateSharpness;
                        //print(string.Format("Sharpness of {0}", pivotTranslateSharpness));
                    }
                    flightCamera.pivotTranslateSharpness = 0;
                    previousPosition = FlightGlobals.ActiveVessel.GetWorldPos3D();
                    // targeting the same part twice will make the camera jump to it
                    hasReachedTarget = raycastTransform == targetTransform;
                    isFocusing = true;
                    targetTransform = raycastTransform;
                }
                else
                {
                    //print("Reset Target");
                    targetTransform = null;
                    hasReachedTarget = false;
                    isFocusing = false;
                    flightCamera.pivotTranslateSharpness = pivotTranslateSharpness;
                }
            }
#if false
            if (Input.GetKeyDown(KeyCode.Y))
            {
                print(string.Format("{0}", targetTransform));
            }
#endif
        }

        void FixedUpdate()
        {
            // do we have a target for the camera focus
            if (targetTransform != null)
            {
                Vector3 positionDifference = flightCamera.transform.parent.position - targetTransform.position;
                float distance = positionDifference.magnitude;

                Vessel vessel = FlightGlobals.ActiveVessel;
                Vector3d currentPosition = vessel.GetWorldPos3D();

                //if (distance >= 0.015f)
                    //print(string.Format("Distance of {0}", distance));

                if (hasReachedTarget || distance < 0.015f)
                {
                    flightCamera.transform.parent.position = targetTransform.position;
                    hasReachedTarget = true;
                }
                else
                {
                    //print(string.Format("Moving by {0}", (positionDifference.normalized * Time.fixedDeltaTime * (distance * Math.Max(4 - distance, 1))).magnitude));
                    flightCamera.transform.parent.position -= positionDifference.normalized * Time.fixedDeltaTime * (distance * Math.Max(4 - distance, 1));
                    // if the parts are not of the same craft, boost the speed at which we move towards it
                    Part part = Part.FromGO(targetTransform.gameObject);
                    if (part != null && part.vessel != vessel)
                        flightCamera.transform.parent.position -= positionDifference.normalized * Time.fixedDeltaTime;
                }

                previousPosition = currentPosition;
            }
            else if (isFocusing)
            {
                hasReachedTarget = false;
                flightCamera.pivotTranslateSharpness = pivotTranslateSharpness;
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
}
