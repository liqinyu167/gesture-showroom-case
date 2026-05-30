using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// 和普通 Say 一样显示对白，但在执行时会在 SaveHistory 里插入一个 Save Point。
    /// 注意：真正读档跳转位置仍然由匹配的 SavePoint 命令或 Label 决定，
    /// 这里只是把“当前剧情节点”记录进 SaveHistory。
    /// </summary>
    [CommandInfo("Narrative",
                 "Say + SavePoint",
                 "Writes text in a dialog box and also adds a Save Point to the Save History.")]
    [AddComponentMenu("")]
    public class SayWithSavePoint : Say
    {
        // 下面这几项完全照 SavePoint 的 Key/Description 设计来，方便你在 Inspector 里同样配置。

        /// <summary>
        /// SavePoint Key 的生成方式（同 SavePoint.KeyMode）。
        /// </summary>
        public SavePoint.KeyMode keyMode = SavePoint.KeyMode.BlockName;

        [Tooltip("当 KeyMode 为 Custom 或 BlockNameAndCustom 时使用的自定义 Key")]
        public string customKey = "";

        [Tooltip("BlockName + Custom 之间的分隔符")]
        public string keySeparator = "_";

        /// <summary>
        /// Description 的生成方式（同 SavePoint.DescriptionMode）。
        /// </summary>
        public SavePoint.DescriptionMode descriptionMode = SavePoint.DescriptionMode.Timestamp;

        [Tooltip("当 DescriptionMode 为 Custom 时使用的描述文本")]
        public string customDescription = "";

        /// <summary>
        /// 生成 SavePoint 的 Key（逻辑照抄 SavePoint.SavePointKey）。
        /// </summary>
        public string SavePointKey
        {
            get
            {
                if (keyMode == SavePoint.KeyMode.BlockName)
                {
                    return ParentBlock != null ? ParentBlock.BlockName : "";
                }
                else if (keyMode == SavePoint.KeyMode.BlockNameAndCustom)
                {
                    return (ParentBlock != null ? ParentBlock.BlockName : "") + keySeparator + customKey;
                }
                else // KeyMode.Custom
                {
                    return customKey;
                }
            }
        }

        /// <summary>
        /// 生成 SavePoint 的描述文本（照 SavePoint.SavePointDescription）。
        /// </summary>
        public string SavePointDescription
        {
            get
            {
                if (descriptionMode == SavePoint.DescriptionMode.Timestamp)
                {
                    // 跟原 SavePoint 一样，用当前时间当说明
                    return System.DateTime.UtcNow.ToString("HH:mm dd MMMM, yyyy");
                }
                else // Custom
                {
                    return customDescription;
                }
            }
        }

        public override void OnEnter()
        {
            // 1）先往 SaveHistory 里插入一个 SavePoint 记录（内存操作，不写硬盘）
            var saveManager = FungusManager.Instance != null ? FungusManager.Instance.SaveManager : null;
            if (saveManager != null)
            {
                saveManager.AddSavePoint(SavePointKey, SavePointDescription);
            }

            // 注意：这里没有调用 SavePointLoaded.NotifyEventHandlers，也没有涉及 CommandIndex + 1
            // 恢复时仍然由 SaveManager / SavePoint / Label 自己处理。

            // 2）再执行原本的 Say 逻辑（显示对白、等待输入等）
            base.OnEnter();
        }

        public override string GetSummary()
        {
            // Inspector 里按钮上的文字，方便你辨认
            string keyInfo = string.IsNullOrEmpty(SavePointKey) ? "(no key)" : SavePointKey;
            return base.GetSummary() + $"  [SavePoint: {keyInfo}]";
        }

        public override bool IsPropertyVisible(string propertyName)
        {
            // 让 Inspector 的显示逻辑跟 SavePoint 一样合理一点
            if (propertyName == "customKey" &&
                keyMode == SavePoint.KeyMode.BlockName)
            {
                return false;
            }

            if (propertyName == "keySeparator" &&
                keyMode != SavePoint.KeyMode.BlockNameAndCustom)
            {
                return false;
            }

            if (propertyName == "customDescription" &&
                descriptionMode != SavePoint.DescriptionMode.Custom)
            {
                return false;
            }

            // 其它字段走 Say 原本的可见性规则
            return base.IsPropertyVisible(propertyName);
        }
    }
}
