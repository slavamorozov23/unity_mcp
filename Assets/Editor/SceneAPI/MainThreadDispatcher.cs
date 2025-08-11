using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneAPI
{
    public static class MainThreadDispatcher
    {
        private static readonly Queue<Action> actions = new Queue<Action>();
        private static bool isInitialized = false;

        public static void Initialize()
        {
            if (!isInitialized)
            {
                EditorApplication.update += Update;
                isInitialized = true;
            }
        }

        public static void Cleanup()
        {
            if (isInitialized)
            {
                EditorApplication.update -= Update;
                isInitialized = false;
                lock (actions)
                {
                    actions.Clear();
                }
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null) return;

            lock (actions)
            {
                actions.Enqueue(action);
            }
        }

        private static void Update()
        {
            lock (actions)
            {
                while (actions.Count > 0)
                {
                    try
                    {
                        actions.Dequeue().Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error executing main thread action: {ex.Message}");
                    }
                }
            }
        }
    }
}