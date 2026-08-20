using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
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
        private readonly Stack<PanelBase> panelStack = new Stack<PanelBase>();
        private readonly Task<ResourceLease<GameObject>> canvasLoadTask;

        public Transform RectTrans_Canvas;
        private Transform bot;
        private Transform mid;
        private Transform top;
        private Transform system;

        public PanelManager()
        {
            canvasLoadTask = AddressableManager.Instance.AcquirePersistentAssetAsync<GameObject>("Canvas");
            PublicMono.Instance.AddListener(CloseTopPanel);
        }

        public async void OpenPanel<T>(string panelName, UILayer layer, UnityAction<T> callback = null) where T : PanelBase
        {
            ResourceScope panelScope = null;
            try
            {
                //校验面板是否存在实例
                if (panelsDic.ContainsKey(panelName)) return;

                //为字典加入面板实例并校验CanvasRoot是否加载完毕
                panelsDic.Add(panelName, null);
                await EnsureCanvasLoaded();

                //获取面板挂点层级
                Transform layerRoot = GetLayerRoot(layer);

                //创建scope资源组
                panelScope = new ResourceScope($"Panel:{panelName}");

                //加载面板资源
                ResourceLease<GameObject> panelLease = await AddressableManager.Instance.AcquireAssetAsync<GameObject>(panelName, panelScope);

                //实例化面板，挂载对应UILayer下，规范实例命名
                GameObject obj = GameObject.Instantiate(panelLease.Asset, layerRoot);
                obj.name = panelName;

                //获取面板脚本，scope注入，注册面板名并压入关闭栈
                T panelScript = obj.GetComponent<T>();
                panelScript.ResourceScope = panelScope;
                panelScript.PanelName = panelName;
                panelsDic[panelName] = panelScript;
                panelStack.Push(panelScript);

                panelScript.DisplayPanel();
                callback?.Invoke(panelScript);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                panelsDic.Remove(panelName);
                panelScope?.Dispose();
            }
        }

        // public async void OpenPanel<T>(string panelName, Transform target, UnityAction<T> callback = null) where T : PanelBase
        // {
        //     if (panelsDic.ContainsKey(panelName)) return;
        //
        //     panelsDic.Add(panelName, null);
        //
        //     ResourceScope panelScope = null;
        //     try
        //     {
        //         Transform staleChild = target.Find(panelName);
        //         if (staleChild != null)
        //             UnityEngine.Object.Destroy(staleChild.gameObject);
        //
        //         panelScope = new ResourceScope($"Panel:{panelName}");
        //
        //         ResourceLease<GameObject> panelLease =
        //             await AddressableManager.Instance.AcquireAssetAsync<GameObject>(panelName, panelScope);
        //         if (panelLease == null || panelLease.Asset == null)
        //             throw new Exception($"Panel load failed: {panelName}");
        //
        //         GameObject obj = GameObject.Instantiate(panelLease.Asset);
        //         obj.name = panelName;
        //         obj.transform.SetParent(target, false);
        //         obj.transform.localScale = Vector3.one;
        //         obj.transform.localPosition = Vector3.zero;
        //
        //         if (obj.transform is RectTransform rectTransform)
        //         {
        //             rectTransform.offsetMax = Vector2.zero;
        //             rectTransform.offsetMin = Vector2.zero;
        //         }
        //
        //         T panelScript = obj.GetComponent<T>();
        //         panelScript.ResourceScope = panelScope;
        //         panelsDic[panelName] = panelScript;
        //         PushPanelToStack(panelName);
        //
        //         panelScript.DisplayPanel();
        //         callback?.Invoke(panelScript);
        //     }
        //     catch (Exception exception)
        //     {
        //         Debug.LogException(exception);
        //         panelsDic.Remove(panelName);
        //         panelScope?.Dispose();
        //     }
        // }

        public void ColsePanel(string panelName, UnityAction callback = null)
        {
            panelsDic.TryGetValue(panelName, out PanelBase panel);
            ColsePanel(panel, callback);
        }

        /// <summary>
        /// 关闭指定面板实例：播退出动画，退出动画放完后由 Timer 延迟统一执行销毁与资源释放。
        /// 面板不在字典中（正在关闭/已关闭/非管理器打开）则忽略，避免重复关闭。
        /// </summary>
        public void ColsePanel(PanelBase panel, UnityAction callback = null)
        {
            if (panel != null && !string.IsNullOrEmpty(panel.PanelName)
                && panelsDic.TryGetValue(panel.PanelName, out PanelBase registered)
                && registered == panel)
            {
                //执行关闭面板的逻辑
                panelsDic.Remove(panel.PanelName);
                panel.HidePanel();

                //面板资源释放与销毁
                UnityAction destroyAndRelease = () =>
                {
                    if (panel == null) return;
                    panel.DestroyPanel();
                    panel.ResourceScope?.Dispose();
                };

                //委托Timer执行面板销毁与资源释放
                if (panel.CloseDelay > 0f) Timer.Instance.AddTimerEvent(panel.CloseDelay, destroyAndRelease);
                else destroyAndRelease();
            }

            callback?.Invoke();
        }

        public T GetPanel<T>(string panelName) where T : PanelBase
        {
            if(panelsDic.ContainsKey(panelName)) return panelsDic[panelName] as T;
            else
            {
                Debug.LogWarning($"[{panelName}]面板不存在");
                return null;
            }
        }

        public void DisplayLayer(UILayer layer)
        {
            Transform layerRoot = GetLayerRoot(layer);
            if (layerRoot != null) layerRoot.gameObject.SetActive(true);
        }

        public void HideLayer(UILayer layer)
        {
            Transform layerRoot = GetLayerRoot(layer);
            if (layerRoot != null) layerRoot.gameObject.SetActive(false);
        }

        private async Task EnsureCanvasLoaded()
        {
            if (RectTrans_Canvas != null) return;

            ResourceLease<GameObject> canvasLease = await canvasLoadTask;
            if (RectTrans_Canvas != null) return;

            if (canvasLease == null || canvasLease.Asset == null) throw new Exception("Canvas load failed.");

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

        private void CloseTopPanel()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            //轮询弹出已销毁（关闭后置空）的面板实例，直到栈顶是存活面板
            while (panelStack.Count > 0 && panelStack.Peek() == null) panelStack.Pop();
            if (panelStack.Count > 0) panelStack.Peek().OnEscapePressed();   // 面板自身的 ESC 逻辑，默认关闭自己
        }
    }
}
