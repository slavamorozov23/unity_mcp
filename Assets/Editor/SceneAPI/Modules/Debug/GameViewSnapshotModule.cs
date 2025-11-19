using System;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;

namespace SceneAPI.Modules
{
    public class GameViewSnapshotModule
    {
        [Serializable]
        public class SnapshotRequest
        {
            public float x;
            public float y;
            public float z;
            public float rotX;
            public float rotY;
            public string lookAtPath; // Опционально: путь к объекту, на который нужно смотреть
            public int width = 1280;
            public int height = 720;
        }

        public string Execute(string requestBody)
        {
            Camera tempCamera = null;
            RenderTexture tempRT = null;
            Texture2D tempTexture = null;

            try
            {
                var req = JsonConvert.DeserializeObject<SnapshotRequest>(requestBody);

                // 1. Создаем временную камеру (или копируем MainCamera)
                GameObject tempCamObj = new GameObject("Temp_Snapshot_Camera");
                tempCamera = tempCamObj.AddComponent<Camera>();
                
                // Настройки камеры
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    tempCamera.CopyFrom(mainCam);
                }
                else
                {
                    tempCamera.clearFlags = CameraClearFlags.Skybox;
                    tempCamera.backgroundColor = Color.gray;
                }

                // 2. Позиционирование
                tempCamObj.transform.position = new Vector3(req.x, req.y, req.z);
                
                if (!string.IsNullOrEmpty(req.lookAtPath))
                {
                    var target = GameObjectUtilities.FindGameObjectByPath(req.lookAtPath);
                    if (target != null)
                    {
                        tempCamObj.transform.LookAt(target.transform);
                    }
                    else
                    {
                        tempCamObj.transform.rotation = Quaternion.Euler(req.rotX, req.rotY, 0);
                    }
                }
                else
                {
                    tempCamObj.transform.rotation = Quaternion.Euler(req.rotX, req.rotY, 0);
                }

                // 3. Рендеринг
                tempRT = RenderTexture.GetTemporary(req.width, req.height, 24);
                tempCamera.targetTexture = tempRT;
                tempCamera.Render();

                RenderTexture.active = tempRT;
                tempTexture = new Texture2D(req.width, req.height, TextureFormat.RGB24, false);
                tempTexture.ReadPixels(new Rect(0, 0, req.width, req.height), 0, 0);
                tempTexture.Apply();

                // 4. Кодирование в Base64 PNG
                byte[] bytes = tempTexture.EncodeToPNG();
                string base64 = Convert.ToBase64String(bytes);

                UnityEngine.Object.DestroyImmediate(tempCamObj);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    width = req.width,
                    height = req.height,
                    format = "png",
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
    }
}