using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI
{
    public class UnitySceneAPIServer
    {
        private HttpListener httpListener;
        private Thread httpListenerThread;
        private bool isRunning = false;
        private int port;
        private SceneAPIHandler apiHandler;

        public bool IsRunning => isRunning;
        public int Port => port;

        public UnitySceneAPIServer(int port)
        {
            this.port = port;
            this.apiHandler = new SceneAPIHandler();
        }

        public bool StartServer()
        {
            MainThreadDispatcher.Initialize();

            try
            {
                httpListener = new HttpListener();
                httpListener.Prefixes.Add($"http://localhost:{port}/");
                httpListener.Start();

                httpListenerThread = new Thread(new ThreadStart(Listen));
                httpListenerThread.IsBackground = true;
                httpListenerThread.Start();

                isRunning = true;
                Debug.Log($"Scene API Server started on port {port}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to start server: {ex.Message}");
                isRunning = false;
                return false;
            }
        }

        public void StopServer()
        {
            isRunning = false;

            if (httpListener != null)
            {
                try
                {
                    httpListener.Stop();
                    httpListener.Close();
                }
                catch { }
                httpListener = null;
            }

            if (httpListenerThread != null)
            {
                try
                {
                    httpListenerThread.Join(100);
                    if (httpListenerThread.IsAlive)
                    {
                        httpListenerThread.Abort();
                    }
                }
                catch { }
                httpListenerThread = null;
            }

            MainThreadDispatcher.Cleanup();
            Debug.Log("Scene API Server stopped");
        }

        private void Listen()
        {
            while (httpListener != null && httpListener.IsListening)
            {
                try
                {
                    IAsyncResult result = httpListener.BeginGetContext(new AsyncCallback(ProcessRequest), httpListener);
                    result.AsyncWaitHandle.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (httpListener != null && httpListener.IsListening)
                    {
                        Debug.LogError($"HTTP Listener error: {ex.Message}");
                    }
                }
            }
        }

        private void ProcessRequest(IAsyncResult result)
        {
            if (httpListener == null || !httpListener.IsListening)
                return;

            try
            {
                HttpListener listener = (HttpListener)result.AsyncState;
                HttpListenerContext context = listener.EndGetContext(result);
                MainThreadDispatcher.Enqueue(() => Process(context));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing request: {ex.Message}");
            }
        }

        private void Process(HttpListenerContext context)
        {
            string response = "";
            string path = context.Request.Url.AbsolutePath;
            string method = context.Request.HttpMethod;

            try
            {
                response = apiHandler.HandleRequest(method, path, context);
            }
            catch (Exception ex)
            {
                response = JsonConvert.SerializeObject(new { error = ex.Message });
            }

            byte[] buffer = Encoding.UTF8.GetBytes(response);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");

            try
            {
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch { }
        }
    }
}