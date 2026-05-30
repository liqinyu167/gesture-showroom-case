// This code is part of the Fungus library (https://github.com/snozbot/fungus)
// It is released for free under the MIT open source license (https://github.com/snozbot/fungus/blob/master/LICENSE)

#if UNITY_5_3_OR_NEWER

using UnityEngine.SceneManagement;
using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// 管理 SaveHistory（存档点列表），并提供一组用于存档与读档的操作。
    /// 
    /// 注意：WebGL 与（已废弃的）WebPlayer 平台无法使用本地 JSON 文件进行持久化，因此会使用 PlayerPrefs 保存数据。
    /// WebGL 若需要强制立即写入文件系统（FS.syncfs），需额外编写 JavaScript。
    /// - WebPlayer 不支持 System.IO 文件读写。
    /// </summary>
    public class SaveManager : MonoBehaviour 
    {
        protected static SaveHistory saveHistory = new SaveHistory();

        public static string STORAGE_DIRECTORY {
            get {
#if UNITY_EDITOR
                // 编辑器模式存到项目目录（Assets/DevSaves）
                return Application.dataPath + "/DevSaves/";
#else
        // 打包版本正常使用 persistentDataPath
        return Application.persistentDataPath + "/FungusSaves/";
#endif
            }
        }

        private static string GetFullFilePath(string saveDataKey)
        {
            return STORAGE_DIRECTORY + saveDataKey + ".json";
        }

        protected virtual bool ReadSaveHistory(string saveDataKey)
        {
            var historyData = string.Empty;
#if UNITY_WEBPLAYER || UNITY_WEBGL
            historyData = PlayerPrefs.GetString(saveDataKey);
#else
            var fullFilePath = GetFullFilePath(saveDataKey);
            if (System.IO.File.Exists(fullFilePath))
            {
                historyData = System.IO.File.ReadAllText(fullFilePath);
            }
#endif//UNITY_WEBPLAYER
            if (!string.IsNullOrEmpty(historyData))
            {
                var tempSaveHistory = JsonUtility.FromJson<SaveHistory>(historyData);
                if (tempSaveHistory != null)
                {
                    saveHistory = tempSaveHistory;
                    return true;
                }
            }

            return false;
        }

        protected virtual bool WriteSaveHistory(string saveDataKey)
        {
            var historyData = JsonUtility.ToJson(saveHistory, true);
            if (!string.IsNullOrEmpty(historyData))
            {
#if UNITY_WEBPLAYER || UNITY_WEBGL
                PlayerPrefs.SetString(saveDataKey, historyData);
                PlayerPrefs.Save();
#else
                var fileLoc = GetFullFilePath(saveDataKey);
                
                //make sure the dir exists
                System.IO.FileInfo file = new System.IO.FileInfo(fileLoc);
                file.Directory.Create();
                
                System.IO.File.WriteAllText(fileLoc, historyData);
#endif//UNITY_WEBPLAYER
                return true;
            }

            return false;
        }

        /// <summary>
        /// 根据 Save Point Key 开始执行 Block。
        /// 执行顺序如下
        /// 1. 每个场景应包含一个 SavePoint 指定为“起始点”。.
        /// 2. 查找所有 Block 中的 SavePoint 命令，只要 Key 匹配且 Resume On Load 启用，即从该 SavePoint 所在命令的下一个命令继续执行。.
        /// 3. 查找所有 Block 中名称与 Key 匹配的 Label，从该 Label 的下一条命令开始执行。.
        /// </summary>
        protected virtual void ExecuteBlocks(string savePointKey)
        {
            // Execute Save Point Loaded event handlers with matching key.
            SavePointLoaded.NotifyEventHandlers(savePointKey);

            // Execute any block containing a SavePoint command matching the save key, with Resume On Load enabled
            var savePoints = UnityEngine.Object.FindObjectsOfType<SavePoint>();
            for (int i = 0; i < savePoints.Length; i++)
            {
                var savePoint = savePoints[i];
                if (savePoint.ResumeOnLoad &&
                    string.Compare(savePoint.SavePointKey, savePointKey, true) == 0)
                {
                    int index = savePoint.CommandIndex;
                    var block = savePoint.ParentBlock;
                    var flowchart = savePoint.GetFlowchart();
                    flowchart.ExecuteBlock(block, index + 1);

                    // Assume there's only one SavePoint using this key
                    break;
                }
            }
        }

        /// <summary>
        /// 从场景中第一个 IsStartPoint 为 true 的 SavePoint 开始执行。
        /// </summary>
        protected virtual void ExecuteStartBlock()
        {
            // 每个场景应包含一个 SavePoint 指定为“起始点”。
            // 当场景以“正常方式”进入时（首次运行、重开场景，或使用
            // Load Scene 命令 / SceneManager.LoadScene），会自动从这个位置执行。

            var savePoints = UnityEngine.Object.FindObjectsOfType<SavePoint>();
            for (int i = 0; i < savePoints.Length; i++)
            {
                var savePoint = savePoints[i];
                if (savePoint.IsStartPoint)
                {
                    savePoint.GetFlowchart().ExecuteBlock(savePoint.ParentBlock, savePoint.CommandIndex);
                    break;
                }
            }
        }

        //读取指定 Key 的存档历史，并加载最新的存档点。
        protected virtual void LoadSavedGame(string saveDataKey)
        {
            if (ReadSaveHistory(saveDataKey))
            {
                saveHistory.ClearRewoundSavePoints();
                saveHistory.LoadLatestSavePoint();
            }
        }

        // Scene loading in Unity is asynchronous so we need to take care to avoid race conditions. 
        // The following callbacks tell us when a scene has been loaded and when 
        // a saved game has been loaded. We delay taking action until the next 
        // frame (via a delegate) so that we know for sure which case we're dealing with.

        /// <summary>
        /// Unity 加载场景是异步的，因此需要避免竞争条件（race condition）。
        /// 以下回调用于监听“场景加载”与“存档加载”事件。
        /// 实际动作会延迟到下一帧执行，确保能区分当前处于哪一种情况。
        /// </summary>
        protected virtual void OnEnable()
        {
            SaveManagerSignals.OnSavePointLoaded += OnSavePointLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected virtual void OnDisable()
        {
            SaveManagerSignals.OnSavePointLoaded -= OnSavePointLoaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        protected virtual void OnSavePointLoaded(string savePointKey)
        {
            var key = savePointKey;
            loadAction = () => ExecuteBlocks(key);
        }

        protected virtual void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Ignore additive scene loads
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            // We first assume that this is a 'normal' scene load rather than a saved game being loaded.
            // If we subsequently receive a notification that a saved game was loaded then the load action 
            // set here will be overridden by the OnSavePointLoaded callback above.

            if (loadAction == null)
            {
                loadAction = ExecuteStartBlock;
            }
        }

        protected System.Action loadAction;

        /// <summary>
        /// 初始场景可能不会触发 OnSceneLoaded，因此 SaveManager 启动时也要确保 执行初始的 Start Block。
        /// </summary>
        protected virtual void Start()
        {
            // The OnSceneLoaded callback above may not be called for the initial scene load in the game,
            // so we call ExecuteStartBlock when the SaveManager starts up too.
            if (loadAction == null)
            {
                loadAction = ExecuteStartBlock;
            }
        }

        /// <summary>
        /// 执行之前预定的加载操作（loadAction）。
        /// 该操作会在执行后立即清空，确保只运行一次。
        /// </summary>
        protected virtual void Update()
        {
            // Execute any previously scheduled load action
            if (loadAction != null)
            {
                loadAction();
                loadAction = null;
            }
        }

        
        #region Public members

        
        /// <summary>
        /// 重开游戏时应加载的场景名称。
        /// </summary>
        public string StartScene { get; set; }

        /// <summary>
        /// 返回当前 SaveHistory 中存档点的数量。
        /// </summary>
        public virtual int NumSavePoints { get { return saveHistory.NumSavePoints; } }

        /// <summary>
        /// 返回当前 SaveHistory 中被“回溯（rewound）”的存档点数量。
        /// </summary>
        public virtual int NumRewoundSavePoints { get { return saveHistory.NumRewoundSavePoints; } }

        /// <summary>
        /// 将 SaveHistory 写入持久化存储。
        /// </summary>
        public virtual void Save(string saveDataKey)
        {
            WriteSaveHistory(saveDataKey);
        }

        /// <summary>
        /// 从持久化存储中加载 SaveHistory，并载入最新的存档点。
        /// 实际加载过程将在下一帧内执行（由 loadAction 驱动）。
        /// </summary>
        public void Load(string saveDataKey)
        {
            // Set a load action to be executed on next update
            var key = saveDataKey;
            loadAction = () => LoadSavedGame(key);
        }

        /// <summary>
        /// 删除指定 Key 的存档历史。
        /// 在 WebGL / WebPlayer 中使用 PlayerPrefs；
        /// 在其他平台则删除对应的 JSON 文件。
        /// </summary>
        public static void Delete(string saveDataKey)
        {
#if UNITY_WEBPLAYER || UNITY_WEBGL
            PlayerPrefs.DeleteKey(saveDataKey);
            PlayerPrefs.Save();
#else
            var fullFilePath = GetFullFilePath(saveDataKey);
            if (System.IO.File.Exists(fullFilePath))
            {
                System.IO.File.Delete(fullFilePath);
            }
#endif//UNITY_WEBPLAYER
        }

        /// <summary>
        /// 返回指定 Key 下是否存在已存储的数据。
        /// WebGL / WebPlayer 使用 PlayerPrefs； 其他平台检查 JSON 文件是否存在。
        /// </summary>
        public bool SaveDataExists(string saveDataKey)
        {
#if UNITY_WEBPLAYER || UNITY_WEBGL
            return PlayerPrefs.HasKey(saveDataKey);
#else
            var fullFilePath = GetFullFilePath(saveDataKey);
            return System.IO.File.Exists(fullFilePath);
#endif//UNITY_WEBPLAYER
            }

        /// <summary>
        /// 使用指定 Key 和描述创建新的存档点，并加入到 SaveHistory 中。
        /// 会触发 SavePointAdded 事件。
        /// </summary>
        public virtual void AddSavePoint(string savePointKey, string savePointDescription)
        {
            saveHistory.AddSavePoint(savePointKey, savePointDescription);

            SaveManagerSignals.DoSavePointAdded(savePointKey, savePointDescription);
        }

        /// <summary>
        /// 回退到上一个存档点并加载它。
        /// 如果只有一个存档点，则不会执行回退操作。
        /// </summary>
        public virtual void Rewind()
        {
            if (saveHistory.NumSavePoints > 0)
            {
                // Rewinding the first save point is not permitted
                if (saveHistory.NumSavePoints > 1)
                {
                    saveHistory.Rewind();
                }

                saveHistory.LoadLatestSavePoint();
            }
        }

        /// <summary>
        /// 快进到下一个被回退过的存档点并加载它。
        /// </summary>
        public virtual void FastForward()
        {
            if (saveHistory.NumRewoundSavePoints > 0)
            {
                saveHistory.FastForward();
                saveHistory.LoadLatestSavePoint();
            }
        }

        /// <summary>
        /// 清空 SaveHistory 中的所有存档点。
        /// </summary>
        public virtual void ClearHistory()
        {
            saveHistory.Clear();
        }

        /// <summary>
        /// 返回调试信息字符串，用于帮助分析存档数据问题。
        /// </summary>
        /// <returns>The debug info.</returns>
        public virtual string GetDebugInfo()
        {
            return saveHistory.GetDebugInfo();
        }

#endregion
    }
}

#endif