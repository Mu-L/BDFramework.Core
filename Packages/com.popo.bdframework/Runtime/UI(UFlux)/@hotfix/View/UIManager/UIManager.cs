﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BDFramework.DataListener;
using UnityEngine;
using BDFramework.Mgr;
using BDFramework.UFlux.WindowStatus;
using LitJson;
using Sirenix.Utilities;


namespace BDFramework.UFlux
{
    public enum UILayer
    {
        Bottom = 0,
        Center,
        Top
    }

    /// <summary>
    /// UI管理类
    /// </summary>
    public partial class UIManager : ManagerBase<UIManager, UIAttribute>
    {
        /// <summary>
        /// UI管理器状态
        /// </summary>
        public AStatusListener Status { get; private set; } = new StatusListenerService();

        /// <summary>
        /// UI窗口字典
        /// </summary>
        private Dictionary<int, IWindow> windowMap = null;

        /// <summary>
        /// ui的三个层级
        /// </summary>
        private Transform Bottom, Center, Top;


        override public void Init()
        {
            //初始化
            windowMap = new Dictionary<int, IWindow>();
            var uiroot = GameObject.Find("UIRoot")?.transform;
            if (uiroot)
            {
                Bottom = uiroot.Find("Bottom")?.transform;
                Center = uiroot.Find("Center")?.transform;
                Top = uiroot.Find("Top")?.transform;
            }
        }

        /// <summary>
        /// 创建一个窗口
        /// </summary>
        /// <param name="uiIdx"></param>
        /// <returns></returns>
        private IWindow CreateWindow(int uiIdx)
        {
            var classData = this.GetClassData(uiIdx);
            if (classData == null)
            {
                Debug.LogError("未注册窗口，无法加载:" + uiIdx);
                return null;
            }

            //根据attribute创建窗口
            var attr = classData.Attribute as UIAttribute;
            var window = Activator.CreateInstance(classData.Type, new object[] {attr.ResourcePath}) as IWindow;
            //设置DI
            SetWindowDI(window);

            //添加窗口消息
            window.State.AddListener<OnWindowOpen>((o) =>
            {
                //窗口
                this.Status.TriggerEvent(new OnWindowOpen(uiIdx));
            });

            window.State.AddListener<OnWindowClose>((o) =>
            {
                //窗口关闭
                this.OnHistoryListChangeByCloseWindow(uiIdx, window);
                this.Status.TriggerEvent(new OnWindowClose(uiIdx));
            });

            window.State.AddListener<OnWindowFocus>((o) =>
            {
                //窗口聚焦
                this.Status.TriggerEvent(new OnWindowFocus(uiIdx));
            });
            window.State.AddListener<OnWindowBlur>((o) =>
            {
                //窗口失焦
                this.Status.TriggerEvent(new OnWindowBlur(uiIdx));
            });


            return window;
        }

        /// <summary>
        /// 设置layer
        /// </summary>
        /// <param name="winCom"></param>
        /// <param name="layer"></param>
        public void Setlayer(IComponent winCom, UILayer layer)
        {
            switch (layer)
            {
                case UILayer.Bottom:
                    winCom.Transform.SetParent(this.Bottom, false);
                    break;
                case UILayer.Center:
                    winCom.Transform.SetParent(this.Center, false);
                    break;
                case UILayer.Top:
                    winCom.Transform.SetParent(this.Top, false);
                    break;
            }
        }

        #region 资源相关处理

        /// <summary>
        /// 加载
        /// </summary>
        /// <param name="uiIndex"></param>
        /// <param name="layer"></param>
        public void LoadWindows(Enum[] uiIdxs, UILayer layer = UILayer.Bottom)
        {
            foreach (var idx in uiIdxs)
            {
                var index = idx.GetHashCode();
                LoadWindow(index, layer);
            }
        }


        /// <summary>
        /// 加载窗口
        /// </summary>
        /// <param name="uiIndexs">窗口枚举</param>
        public void LoadWindow(Enum uiIndex, UILayer layer = UILayer.Bottom)
        {
            var index = uiIndex.GetHashCode();
            LoadWindow(index, layer);
        }

        /// <summary>
        /// 加载窗口
        /// </summary>
        /// <param name="index"></param>
        private void LoadWindow(int index, UILayer layer = UILayer.Bottom)
        {
            if (windowMap.ContainsKey(index))
            {
                var win = windowMap[index] as IComponent;
                if (win.IsLoad)
                {
                    BDebug.Log("已经加载过并未卸载" + index, Color.red);
                }
            }
            else
            {
                //创建ui
                var window = CreateWindow(index) as IComponent;
                if (window == null)
                {
                    BDebug.Log("不存在UI:" + index, Color.red);
                }
                else
                {
                    windowMap[index] = window as IWindow;
                    window.Load();
                    window.Transform.gameObject.SetActive(false);
                    Setlayer(window, layer);
                    //推送缓存的数据
                    PushCaheData(index);
                }
            }
        }


        /// <summary>
        /// 加载窗口
        /// </summary>
        /// <param name="uiIndexs">窗口枚举</param>
        public void AsyncLoadWindow(Enum uiIndex, Action callback)
        {
            var index = uiIndex.GetHashCode();

            if (windowMap.ContainsKey(index))
            {
                var uvalue = windowMap[index] as IComponent;
                if (uvalue.IsLoad)
                {
                    BDebug.Log("已经加载过并未卸载" + index, Color.red);
                }
            }
            else
            {
                //创建ui
                var window = CreateWindow(index) as IComponent;
                if (window == null)
                {
                    BDebug.Log("不存在UI:" + index, Color.red);
                }
                else
                {
                    windowMap[index] = window as IWindow;
                    //开始窗口加载
                    window.AsyncLoad(() =>
                    {
                        if (window.Transform)
                        {
                            window.Transform.gameObject.SetActive(false);
                            window.Transform.SetParent(this.Bottom, false);
                        }

                        //推送缓存的数据
                        PushCaheData(index);
                        //回调
                        callback?.Invoke();
                    });
                }
            }
        }

        /// <summary>
        /// 异步加载窗口
        /// Todo 这里list<enum>  ilr解释下会报错，所以用list int传参
        /// </summary>
        /// <param name="idxs"></param>
        /// <param name="loadProcessAction"></param>
        public void AsyncLoadWindows(List<int> idxs, Action<int, int> loadProcessAction)
        {
            BDebug.Log("异步加载窗口:" + JsonMapper.ToJson(idxs));
            //
            int totalWindowsNum = idxs.Count;
            int curTaskCount = 0;
            foreach (var index in idxs)
            {
                if (windowMap.ContainsKey(index))
                {
                    var uvalue = windowMap[index] as IComponent;
                    if (uvalue.IsLoad)
                    {
                        Debug.LogError("已经加载过并未卸载" + index);
                        //任务直接完成
                        curTaskCount++;
                        loadProcessAction(totalWindowsNum, curTaskCount);
                    }
                }
                else
                {
                    //创建窗口
                    var window = CreateWindow(index);
                    if (window == null)
                    {
                        Debug.LogErrorFormat("不存在UI:{0}", index);
                        curTaskCount++;
                        loadProcessAction(totalWindowsNum, curTaskCount);
                    }
                    else
                    {
                        windowMap[index] = window;
                        var com = window as IComponent;
                        //开始窗口加载
                        com.AsyncLoad(() =>
                        {
                            curTaskCount++;
                            if (com.Transform)
                            {
                                com.Transform.gameObject.SetActive(false);
                                com.Transform.SetParent(this.Bottom, false);
                            }

                            //推送缓存的数据
                            PushCaheData(index);
                            //回调
                            loadProcessAction?.Invoke(totalWindowsNum, curTaskCount);
                        });
                    }
                }
            }
        }


        /// <summary>
        /// 卸载窗口
        /// </summary>
        /// <param name="idxs">窗口枚举</param>
        public void UnLoadWindows(List<Enum> idxs)
        {
            foreach (var i in idxs)
            {
                UnLoadWindow(i);
            }
        }

        /// <summary>
        /// 卸载窗口
        /// </summary>
        /// <param name="indexs">窗口枚举</param>
        public void UnLoadWindow(Enum index)
        {
            var _index = index.GetHashCode();
            if (windowMap.ContainsKey(_index))
            {
                var winCom = windowMap[_index] as IComponent;
                winCom.Close();
                winCom.Destroy();
                windowMap.Remove(_index);
            }
            else
            {
                Debug.LogErrorFormat("不存在UI：{0}", _index);
            }
        }

        /// <summary>
        /// 卸载窗口
        /// </summary>
        public void UnLoadALLWindows()
        {
            foreach (var v in this.windowMap.Values)
            {
                var vcom = v as IComponent;
                vcom.Close();
                vcom.Destroy();
            }

            ClearHistory();
            this.windowMap.Clear();
            this.uiDataCacheMap.Clear();
        }

        /// <summary>
        /// 清理导航历史
        /// </summary>
        public void ClearHistory()
        {
            HistoryList.Clear();
        }

        #endregion

        #region 打开、关闭

        /// <summary>
        /// 显示窗口
        /// </summary>
        /// <param name="uiMsgData"></param>
        /// <param name="isAddToHistory"></param>
        /// <typeparam name="T"></typeparam>
        public void ShowWindow<T>(UIMsgData uiMsgData = null, bool isAddToHistory = true) where T : IWindow
        {
            var type = typeof(T);
            var cd = GetClassData(type);
            if (cd == null)
            {
                var _attrs = type.GetCustomAttributes<UIAttribute>(false);
                this.RegisterTypes(type, _attrs.ToArray());
                cd = GetClassData(type);
            }

            //显示窗口
            var attr = cd.Attribute as UIAttribute;
            ShowWindow(attr.IntTag, uiMsgData, isAddToHistory);
        }


        /// <summary>
        /// 显示窗口
        /// </summary>
        /// <param name="uiEnumIdx">ui枚举</param>
        /// <param name="layer">显示的层级</param>
        /// <param name="uiMsgData"></param>
        /// <param name="isAddToHistory"></param>
        public void ShowWindow(Enum uiEnumIdx, UILayer layer, UIMsgData uiMsgData = null, bool isAddToHistory = true)
        {
            int uiIdx = uiEnumIdx.GetHashCode();
            var winCom = this.ShowWindow(uiIdx, uiMsgData, isAddToHistory);
            Setlayer(winCom, layer);
        }

        /// <summary>
        ///  显示窗口
        /// </summary>
        /// <param name="uiEnumIdx"></param>
        /// <param name="uiMsgData"></param>
        /// <param name="isAddToHistory"></param>
        public void ShowWindow(Enum uiEnumIdx, UIMsgData uiMsgData = null, bool isAddToHistory = true)
        {
            int uiIdx = uiEnumIdx.GetHashCode();
            this.ShowWindow(uiIdx, uiMsgData, isAddToHistory);
        }

        /// <summary>
        /// 显示
        /// </summary>
        /// <param name="uiIdx"></param>
        /// <param name="uiMsgData"></param>
        /// <param name="isAddToHistory"></param>
        private IComponent ShowWindow(int uiIdx, UIMsgData uiMsgData = null, bool isAddToHistory = true)
        {
            if (!windowMap.ContainsKey(uiIdx))
            {
                BDebug.Log($"未加载UI：{uiIdx},开始同步加载");
                //同步加载
                BDebug.LogWatchBegin($"加载{uiIdx}");
                LoadWindow(uiIdx);
                BDebug.LogWatchEnd($"加载{uiIdx}");
            }

            //
            var win = windowMap[uiIdx];
            var winCom = win as IComponent;
            if (!winCom.IsOpen && winCom.IsLoad)
            {
                //effect
                if (isAddToHistory)
                {
                    AddToHistory(uiIdx);
                }

                winCom.Transform.SetAsLastSibling();
                win.Open(uiMsgData);
            }
            else
            {
                Debug.LogErrorFormat("UI处于[unload,lock,open]状态之一：{0}", uiIdx);
            }

            return winCom;
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="uiIndex">窗口枚举</param>
        public void CloseWindow(Enum uiEnumIdx)
        {
            var idx = uiEnumIdx.GetHashCode();

            this.CloseWindow(idx);
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="uiIdx"></param>
        public void CloseWindow(int uiIdx)
        {
            if (windowMap.ContainsKey(uiIdx))
            {
                var win = windowMap[uiIdx];
                var winCom = win as IComponent;
                if (winCom.IsOpen && winCom.IsLoad)
                {
                    win.Close();
                }
                else
                {
                    Debug.LogErrorFormat("UI未加载或已经处于close状态：{0}", uiIdx);
                }
            }
            else
            {
                Debug.LogErrorFormat("不存在UI：{0}", uiIdx);
            }
        }

        #endregion

        #region 导航前进、后退

        /// <summary>
        /// 当前导航UI
        /// </summary>
        private int curForwardBackUI = -1;

        /// <summary>
        /// 当前导航下标
        /// </summary>
        private int curForwardBackUIIdx = 0;

        /// <summary>
        /// 往前
        /// </summary>
        public void Forward()
        {
            var uiIdex = HistoryList[curForwardBackUIIdx];
            //说明列表发生了变化,重设到栈顶
            if (curForwardBackUI != uiIdex)
            {
                curForwardBackUIIdx = HistoryList.Count - 1;
                curForwardBackUI = HistoryList[curForwardBackUIIdx];
            }

            if (curForwardBackUIIdx < HistoryList.Count - 1)
            {
                curForwardBackUIIdx++;
                curForwardBackUI = this.HistoryList[curForwardBackUIIdx];
                this.ShowWindow(curForwardBackUI, isAddToHistory: false);
            }
            else
            {
                BDebug.LogError("已经是顶部");
            }
        }

        /// <summary>
        /// 后退
        /// </summary>
        public void Back()
        {
            var uiIdex = HistoryList[curForwardBackUIIdx];
            //说明列表发生了变化,重设到栈顶
            if (curForwardBackUI != uiIdex)
            {
                curForwardBackUIIdx = HistoryList.Count - 1;
                curForwardBackUI = HistoryList[curForwardBackUIIdx];
            }

            if (curForwardBackUIIdx > 0)
            {
                curForwardBackUIIdx++;
                curForwardBackUI = this.HistoryList[curForwardBackUIIdx];
                this.ShowWindow(curForwardBackUI, isAddToHistory: false);
            }
            else
            {
                BDebug.LogError("已经是底部");
            }
        }

        #endregion

        #region 窗口队列的维护

        static private int MAX_HISTORY_NUM = 50;

        /// <summary>
        /// 历史列表
        /// 永远不会重复
        /// </summary>
        public List<int> HistoryList { get; private set; } = new List<int>(MAX_HISTORY_NUM);

        /// <summary>
        /// 添加到历史列表
        /// </summary>
        /// <param name="uiIdx"></param>
        private void AddToHistory(int uiIdx)
        {
            if (HistoryList.Count == MAX_HISTORY_NUM)
            {
                HistoryList.RemoveAt(0);
            }

            //保证不会有重复列表
            HistoryList.Remove(uiIdx);
            HistoryList.Add(uiIdx);

            //Focus当前窗口
            this.windowMap[uiIdx].OnFocus();
            //Blur上一个窗口
            if (HistoryList.Count > 2)
            {
                var lastWinIdx = HistoryList[HistoryList.Count - 2];
                this.windowMap[lastWinIdx].OnBlur();
            }
        }


        /// <summary>
        /// 当窗口关闭
        /// </summary>
        private void OnHistoryListChangeByCloseWindow(int uiIdx, IWindow window)
        {
            if (HistoryList.Count > 2)
            {
                bool isCheckFocus = false;
                for (int i = HistoryList.Count - 1; i >= 0; i--)
                {
                    var idx = HistoryList[i];
                    this.windowMap.TryGetValue(idx, out var win);
                    if (win == null)
                    {
                        continue;
                    }

                    win = this.windowMap[idx];
                    var winCom = win as IComponent;
                    //判断栈顶是否有关闭的,有则继续搜索第一个打开的执行focus，
                    if (!winCom.IsOpen)
                    {
                        isCheckFocus = true;
                        HistoryList.Remove(idx); //移除栈顶
                    }
                    else if (winCom.IsOpen && !win.IsFocus && isCheckFocus)
                    {
                        //这里Reopen 是为了解决窗口后退中 相同窗口反复打开，导致层级变化的问题
                        win.OnFocus();
                        break;
                    }
                }
            }
        }

        #endregion

        #region 推送消息

        /// <summary>
        /// 推送缓存信息
        /// </summary>
        /// <param name="uiIdx"></param>
        private void PushCaheData(int uiIdx)
        {
            // return;
            //检查ui数据缓存
            List<UIMsgData> cacheList = null;
            uiDataCacheMap.TryGetValue(uiIdx, out cacheList);
            if (cacheList != null)
            {
                for (int i = 0; i < cacheList.Count; i++)
                {
                    var data = cacheList[i];

                    windowMap[uiIdx].SendMessage(data);
                    BDebug.Log("push cache data " + uiIdx);
                }

                cacheList.Clear();
                BDebug.LogFormat("推送数据：{0} ,{1}条", uiIdx, cacheList.Count);
            }
        }

        private Dictionary<int, List<UIMsgData>> uiDataCacheMap = new Dictionary<int, List<UIMsgData>>();

        /// <summary>
        /// 外部推送ui数据
        /// </summary>
        /// <param name="uiIndex"></param>
        /// <param name="uiMsg"></param>
        public void SendMessage(Enum index, UIMsgData uiMsg)
        {
            var uiIndex = index.GetHashCode();
            IWindow win;
            if (windowMap.TryGetValue(uiIndex, out win))
            {
                win.SendMessage(uiMsg);
                return;
            }

            //存入缓存
            List<UIMsgData> list = null;
            uiDataCacheMap.TryGetValue(uiIndex, out list);
            //
            if (list == null)
            {
                list = new List<UIMsgData>();
                uiDataCacheMap[uiIndex] = list;
            }

            list.Add(uiMsg);
        }

        #endregion

        #region 对外接口

        /// <summary>
        /// 获取一个窗口
        /// </summary>
        /// <param name="uiIndex"></param>
        /// <returns></returns>
        public IWindow GetWindow(Enum uiIndex)
        {
            var index = uiIndex.GetHashCode();
            IWindow win = null;
            this.windowMap.TryGetValue(index, out win);
            return win;
        }

        #endregion
    }
}
