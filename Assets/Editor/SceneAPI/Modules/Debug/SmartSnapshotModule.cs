using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class SmartSnapshotModule
    {
        [Serializable]
        public class SmartSnapshotRequest
        {
            public string[] targetPaths;
            public int width = 1280;
            public int height = 720;
            public float distance = 10f;
            public int angularSteps = 12;
            public float minElevation = 15f;
            public float maxElevation = 60f;
        }

        public string Execute(string requestBody)
        {
            Camera tempCamera = null;
            RenderTexture tempRT = null;
            Texture2D tempTexture = null;

            try
            {
                var req = JsonConvert.DeserializeObject<SmartSnapshotRequest>(requestBody);
                
                if (req.targetPaths == null || req.targetPaths.Length == 0)
                    return JsonConvert.SerializeObject(new { success = false, error = "No target paths provided" });

                // Sync Physics to ensure Raycasts work against recently moved objects in Edit Mode
                Physics.SyncTransforms();

                List<GameObject> targets = new List<GameObject>();
                Bounds totalBounds = new Bounds();
                bool first = true;

                foreach (var path in req.targetPaths)
                {
                    var go = GameObjectUtilities.FindGameObjectByPath(path);
                    if (go != null)
                    {
                        targets.Add(go);
                        Bounds b = GetObjectBounds(go);
                        if (first) { totalBounds = b; first = false; }
                        else { totalBounds.Encapsulate(b); }
                    }
                }

                if (targets.Count == 0)
                    return JsonConvert.SerializeObject(new { success = false, error = "None of the target objects were found" });

                Vector3 centerPoint = totalBounds.center;

                GameObject tempCamObj = new GameObject("Temp_Smart_Camera");
                tempCamera = tempCamObj.AddComponent<Camera>();
                var mainCam = Camera.main;
                if (mainCam != null) tempCamera.CopyFrom(mainCam);
                else {
                     tempCamera.clearFlags = CameraClearFlags.Skybox;
                     tempCamera.backgroundColor = Color.gray;
                }

                // Search for best view
                Vector3 bestPosition = centerPoint - Vector3.forward * req.distance + Vector3.up * 5;
                float bestScore = -1;
                int maxVisiblePoints = 0;

                Dictionary<GameObject, List<Vector3>> targetTestPoints = new Dictionary<GameObject, List<Vector3>>();
                foreach(var t in targets)
                {
                    Bounds b = GetObjectBounds(t);
                    targetTestPoints[t] = new List<Vector3> { 
                        b.center, b.max, b.min,
                        new Vector3(b.min.x, b.center.y, b.center.z),
                        new Vector3(b.max.x, b.center.y, b.center.z)
                    };
                }

                for (float elev = req.minElevation; elev <= req.maxElevation; elev += (req.maxElevation - req.minElevation) / 3f)
                {
                    for (int i = 0; i < req.angularSteps; i++)
                    {
                        float angle = i * (360f / req.angularSteps);
                        Quaternion rot = Quaternion.Euler(elev, angle, 0);
                        Vector3 candidatePos = centerPoint + (rot * -Vector3.forward * req.distance);

                        tempCamera.transform.position = candidatePos;
                        tempCamera.transform.LookAt(centerPoint);

                        int currentVisiblePoints = 0;
                        int visibleObjectsCount = 0;

                        foreach (var target in targets)
                        {
                            bool objectVisible = false;
                            foreach(var point in targetTestPoints[target])
                            {
                                Vector3 vp = tempCamera.WorldToViewportPoint(point);
                                if (vp.z > 0 && vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1)
                                {
                                    Vector3 dir = point - candidatePos;
                                    // Raycast check for occlusion
                                    if (!Physics.Raycast(candidatePos, dir, out RaycastHit hit, dir.magnitude - 0.1f) || 
                                        hit.collider.gameObject == target || 
                                        hit.collider.transform.IsChildOf(target.transform))
                                    {
                                        currentVisiblePoints++;
                                        objectVisible = true;
                                    }
                                }
                            }
                            if (objectVisible) visibleObjectsCount++;
                        }

                        float score = currentVisiblePoints + (visibleObjectsCount * 5) - (Mathf.Abs(angle % 90) * 0.01f);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPosition = candidatePos;
                            maxVisiblePoints = visibleObjectsCount;
                        }
                    }
                }

                // Final Render
                tempCamera.transform.position = bestPosition;
                tempCamera.transform.LookAt(centerPoint);

                tempRT = RenderTexture.GetTemporary(req.width, req.height, 24);
                tempCamera.targetTexture = tempRT;
                tempCamera.Render();

                RenderTexture.active = tempRT;
                tempTexture = new Texture2D(req.width, req.height, TextureFormat.RGB24, false);
                tempTexture.ReadPixels(new Rect(0, 0, req.width, req.height), 0, 0);
                tempTexture.Apply();

                string base64 = Convert.ToBase64String(tempTexture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tempCamObj);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    visibleObjectsCount = maxVisiblePoints,
                    cameraPos = new { x = bestPosition.x, y = bestPosition.y, z = bestPosition.z },
                    base64Image = base64
                });
            }
            catch (Exception ex)
            {
                if (tempCamera != null) UnityEngine.Object.DestroyImmediate(tempCamera.gameObject);
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
            finally
            {
                if (tempRT != null) RenderTexture.ReleaseTemporary(tempRT);
                if (tempTexture != null) UnityEngine.Object.DestroyImmediate(tempTexture);
                RenderTexture.active = null;
            }
        }

        private Bounds GetObjectBounds(GameObject go)
        {
            var r = go.GetComponentInChildren<Renderer>();
            if (r != null) return r.bounds;
            var c = go.GetComponentInChildren<Collider>();
            if (c != null) return c.bounds;
            return new Bounds(go.transform.position, Vector3.one * 0.5f);
        }
    }
}