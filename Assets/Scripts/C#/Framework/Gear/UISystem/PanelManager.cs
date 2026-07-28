using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Framework.Core
{
    /// <summary>
    /// UI layer slots under the shared Canvas root.
    /// </summary>
    public enum UILayer
    {
        Bot,
        Mid,
        Top,
        System,
    }

    /// <summary>
    /// Centralized panel lifecycle and Canvas layer access.
    /// </summary>
    public class PanelManager
    {
        private static readonly PanelManager instance = new PanelManager();
        public static PanelManager Instance => instance;

        private readonly Dictionary<string, PanelBase> panelsDic = new Dictionary<string, PanelBase>();
        private readonly Dictionary<string, ResourceScope> panelScopes = new Dictionary<string, ResourceScope>();
        private readonly Stack<string> panelStack = new Stack<string>();
        private readonly Task<ResourceLease<GameObject>> canvasLoadTask;

        public Transform RectTrans_Canvas;
        private Transform bot;
        private Transform mid;
        private Transform top;
        private Transform system;

        public PanelManager()
        {
            canvasLoadTask = AddressableManager.Instance.AcquirePersistentAssetAsync<GameObject>("Canvas");
            PublicMono.Instance.AddListener(CheckCloseTopPanelInput);
        }

        public async void PanelDisplay<T>(string panelName, UILayer layer, UnityAction<T> callback = null) where T : PanelBase
        {
            try
            {
                if (panelsDic.ContainsKey(panelName))
                    return;

                panelsDic.Add(panelName, null);
                await EnsureCanvasReady();

                Transform layerRoot = GetLayerRoot(layer);
                Transform staleChild = layerRoot.Find(panelName);
                if (staleChild != null)
                    UnityEngine.Object.Destroy(staleChild.gameObject);

                ResourceScope panelScope = new ResourceScope($"Panel:{panelName}");
                panelScopes[panelName] = panelScope;

                ResourceLease<GameObject> panelLease =
                    await AddressableManager.Instance.AcquireAssetAsync<GameObject>(panelName, panelScope);
                if (panelLease == null || panelLease.Asset == null)
                    throw new Exception($"Panel load failed: {panelName}");

                GameObject obj = GameObject.Instantiate(panelLease.Asset);
                obj.name = panelName;
                obj.transform.SetParent(layerRoot, false);

                T panelScript = obj.GetComponent<T>();
                panelsDic[panelName] = panelScript;
                PushPanelToStack(panelName);

                panelScript.DisplayPanel();
                callback?.Invoke(panelScript);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                panelsDic.Remove(panelName);
                DisposePanelScope(panelName);
            }
        }

        public async void PanelDisplay<T>(string panelName, Transform target, UnityAction<T> callback = null) where T : PanelBase
        {
            if (panelsDic.ContainsKey(panelName))
                return;

            panelsDic.Add(panelName, null);

            try
            {
                Transform staleChild = target.Find(panelName);
                if (staleChild != null)
                    UnityEngine.Object.Destroy(staleChild.gameObject);

                ResourceScope panelScope = new ResourceScope($"Panel:{panelName}");
                panelScopes[panelName] = panelScope;

                ResourceLease<GameObject> panelLease =
                    await AddressableManager.Instance.AcquireAssetAsync<GameObject>(panelName, panelScope);
                if (panelLease == null || panelLease.Asset == null)
                    throw new Exception($"Panel load failed: {panelName}");

                GameObject obj = GameObject.Instantiate(panelLease.Asset);
                obj.name = panelName;
                obj.transform.SetParent(target, false);
                obj.transform.localScale = Vector3.one;
                obj.transform.localPosition = Vector3.zero;

                if (obj.transform is RectTransform rectTransform)
                {
                    rectTransform.offsetMax = Vector2.zero;
                    rectTransform.offsetMin = Vector2.zero;
                }

                T panelScript = obj.GetComponent<T>();
                panelsDic[panelName] = panelScript;
                PushPanelToStack(panelName);

                panelScript.DisplayPanel();
                callback?.Invoke(panelScript);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                panelsDic.Remove(panelName);
                DisposePanelScope(panelName);
            }
        }

        public void PanelHide(string panelName, UnityAction callback = null)
        {
            if (panelsDic.TryGetValue(panelName, out PanelBase panel))
            {
                panel.HidePanel();
                panelsDic.Remove(panelName);
                RemovePanelFromStack(panelName);
            }

            DisposePanelScope(panelName);
            callback?.Invoke();
        }

        public T GetPanel<T>(string panelName) where T : PanelBase
        {
            panelsDic.TryGetValue(panelName, out PanelBase panel);
            return panel as T;
        }

        public void ChangeCanvasRenderMode(RenderMode renderMode)
        {
            if (RectTrans_Canvas != null)
                RectTrans_Canvas.GetComponent<Canvas>().renderMode = renderMode;
        }

        public void DisplayLayer(UILayer layer)
        {
            Transform layerRoot = GetLayerRoot(layer);
            if (layerRoot != null)
                layerRoot.gameObject.SetActive(true);
        }

        public void HideLayer(UILayer layer)
        {
            Transform layerRoot = GetLayerRoot(layer);
            if (layerRoot != null)
                layerRoot.gameObject.SetActive(false);
        }

        public void CloseTopPanel()
        {
            if (panelStack.Count == 0)
                return;

            PanelHide(panelStack.Peek());
        }

        private async Task EnsureCanvasReady()
        {
            if (RectTrans_Canvas != null)
                return;

            ResourceLease<GameObject> canvasLease = await canvasLoadTask;
            if (RectTrans_Canvas != null)
                return;

            if (canvasLease == null || canvasLease.Asset == null)
                throw new Exception("Canvas load failed.");

            GameObject obj = GameObject.Instantiate(canvasLease.Asset);
            RectTrans_Canvas = obj.transform;
            GameObject.DontDestroyOnLoad(obj);

            bot = RectTrans_Canvas.Find("Bot");
            mid = RectTrans_Canvas.Find("Mid");
            top = RectTrans_Canvas.Find("Top");
            system = RectTrans_Canvas.Find("System");
        }

        private Transform GetLayerRoot(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.Bot: return bot;
                case UILayer.Mid: return mid;
                case UILayer.Top: return top;
                case UILayer.System:
                default: return system;
            }
        }

        private void PushPanelToStack(string panelName)
        {
            RemovePanelFromStack(panelName);
            panelStack.Push(panelName);
        }

        private void RemovePanelFromStack(string panelName)
        {
            if (panelStack.Count == 0)
                return;

            Stack<string> tempStack = new Stack<string>();
            while (panelStack.Count > 0)
            {
                string current = panelStack.Pop();
                if (current != panelName)
                    tempStack.Push(current);
            }

            while (tempStack.Count > 0)
                panelStack.Push(tempStack.Pop());
        }

        private void DisposePanelScope(string panelName)
        {
            if (!panelScopes.TryGetValue(panelName, out ResourceScope scope))
                return;

            scope.Dispose();
            panelScopes.Remove(panelName);
        }

        private void CheckCloseTopPanelInput()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            if (panelStack.Count == 0)
                return;

            string topPanelName = panelStack.Peek();
            if (panelsDic.TryGetValue(topPanelName, out PanelBase panel) && !panel.OnEscapePressed())
                return;

            CloseTopPanel();
        }
    }
}
