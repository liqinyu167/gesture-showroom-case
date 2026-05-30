// This code is part of the Fungus library (https://github.com/snozbot/fungus)
// It is released for free under the MIT open source license (https://github.com/snozbot/fungus/blob/master/LICENSE)

using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Fungus
{
    /// <summary>
    /// Execution state of a Block.
    /// </summary>
    public enum ExecutionState
    {
        /// <summary> No command executing </summary>
        Idle,       
        /// <summary> Executing a command </summary>
        Executing,
    }

    /// <summary>
    /// A container for a sequence of Fungus comands.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Flowchart))]
    [AddComponentMenu("")]
    public class Block : Node
    {
        [SerializeField] protected int itemId = -1; // Invalid flowchart item id

        [FormerlySerializedAs("sequenceName")]
        [Tooltip("The name of the block node as displayed in the Flowchart window")]
        [SerializeField] protected string blockName = "New Block";

        [TextArea(2, 5)]
        [Tooltip("Description text to display under the block node")]
        [SerializeField] protected string description = "";

        [Tooltip("An optional Event Handler which can execute the block when an event occurs")]
        [SerializeField] protected EventHandler eventHandler;

        [SerializeField] protected List<Command> commandList = new List<Command>();

        protected ExecutionState executionState;

        protected Command activeCommand;

        protected Action lastOnCompleteAction;

        /// <summary>
        // Index of last command executed before the current one.
        // -1 indicates no previous command.
        /// </summary>
        protected int previousActiveCommandIndex = -1;

        public int PreviousActiveCommandIndex { get { return previousActiveCommandIndex; } }

        protected int jumpToCommandIndex = -1;

        protected int executionCount;

        protected bool executionInfoSet = false;

        /// <summary>
        /// If set, flowchart will not auto select when it is next executed, used by eventhandlers.
        /// Only effects the editor.
        /// </summary>
        public bool SuppressNextAutoSelection { get; set; }

        [SerializeField] bool suppressAllAutoSelections = false;
        

        protected virtual void Awake()
        {
            SetExecutionInfo();
        }

        /// <summary>
        /// Populate the command metadata used to control execution.
        /// </summary>
        protected virtual void SetExecutionInfo()
        {
            // Give each child command a reference back to its parent block
            // and tell each command its index in the list.
            int index = 0;
            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                if (command == null)
                {
                    continue;
                }
                command.ParentBlock = this;
                command.CommandIndex = index++;
            }

            // Ensure all commands are at their correct indent level
            // This should have already happened in the editor, but may be necessary
            // if commands are added to the Block at runtime.
            UpdateIndentLevels();

            executionInfoSet = true;
        }

#if UNITY_EDITOR
        // The user can modify the command list order while playing in the editor,
        // so we keep the command indices updated every frame. There's no need to
        // do this in player builds so we compile this bit out for those builds.
        protected virtual void Update()
        {
            int index = 0;
            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                if (command == null)// Null entry will be deleted automatically later
                
                {
                    continue;
                }
                command.CommandIndex = index++;
            }
        }

#endif
        //editor only state for speeding up flowchart window drawing
        public bool IsSelected { get; set; }    //local cache of selectedness
        public enum FilteredState { Full, Partial, None}
        public FilteredState FilterState { get; set; }    //local cache of filteredness
        public bool IsControlSelected { get; set; } //local cache of being part of the control exclusion group

        #region 公共成员

        /// <summary>
        /// 块的执行状态。
        /// </summary>
        public virtual ExecutionState State { get { return executionState; } }

        /// <summary>
        /// 块的唯一标识符。
        /// </summary>
        public virtual int ItemId { get { return itemId; } set { itemId = value; } }

        /// <summary>
        /// 流程图窗口中显示的块节点名称。
        /// </summary>
        public virtual string BlockName { get { return blockName; } set { blockName = value; } }

        /// <summary>
        /// 显示在块节点下方的描述文本。
        /// </summary>
        public virtual string Description { get { return description; } }

        /// <summary>
        /// 可选的事件处理器，可在事件发生时执行该块。
        /// 注意：此处使用具体类而非接口，是由于编辑器的特殊行为限制。
        /// </summary>
        public virtual EventHandler _EventHandler { get { return eventHandler; } set { eventHandler = value; } }

        /// <summary>
        /// 当前正在执行的命令。
        /// </summary>
        public virtual Command ActiveCommand { get { return activeCommand; } }

        /// <summary>
        /// 块执行图标的淡出计时器。
        /// </summary>
        public virtual float ExecutingIconTimer { get; set; }

        /// <summary>
        /// 序列中的命令列表。
        /// </summary>
        public virtual List<Command> CommandList { get { return commandList; } }

        /// <summary>
        /// 控制块执行协程中待执行的下一条命令。
        /// </summary>
        public virtual int JumpToCommandIndex { set { jumpToCommandIndex = value; } }

        /// <summary>
        /// 返回该块所属的父流程图。
        /// </summary>
        public virtual Flowchart GetFlowchart()
        {
            return GetComponent<Flowchart>();
        }

        /// <summary>
        /// 若块正在执行命令，则返回 true。
        /// </summary>
        public virtual bool IsExecuting()
        {
            return (executionState == ExecutionState.Executing);
        }

        /// <summary>
        /// 返回该块的执行次数。
        /// </summary>
        public virtual int GetExecutionCount()
        {
            return executionCount;
        }

        /// <summary>
        /// 启动一个协程，执行块中的所有命令。每个块仅允许同时运行一个实例。
        /// </summary>
        public virtual void StartExecution()
        {
            StartCoroutine(Execute());
        }

        /// <summary>
        /// 执行块中所有命令的协程方法。每个块仅允许同时运行一个实例。
        /// </summary>
        /// <param name="commandIndex">开始执行的命令索引</param>
        /// <param name="onComplete">执行完成时调用的委托函数</param>
        public virtual IEnumerator Execute(int commandIndex = 0, Action onComplete = null)
        {
            if (executionState != ExecutionState.Idle)
            {
                Debug.LogWarning(BlockName + " 无法执行，它已处于运行中状态。");
                yield break;
            }

            lastOnCompleteAction = onComplete;

            if (!executionInfoSet)
            {
                SetExecutionInfo();
            }

            executionCount++;
            var executionCountAtStart = executionCount;

            var flowchart = GetFlowchart();
            executionState = ExecutionState.Executing;
            BlockSignals.DoBlockStart(this);

            bool suppressSelectionChanges = false;

            #if UNITY_EDITOR
            // 选中正在执行的块和第一条命令
            if (suppressAllAutoSelections || SuppressNextAutoSelection)
            {
                SuppressNextAutoSelection = false;
                suppressSelectionChanges = true;
            }
            else
            {
                flowchart.SelectedBlock = this;
                if (commandList.Count > 0)
                {
                    flowchart.ClearSelectedCommands();
                    flowchart.AddSelectedCommand(commandList[0]);
                }
            }
            #endif

            jumpToCommandIndex = commandIndex;

            int i = 0;
            while (true)
            {
                // 执行中的命令可通过 Command.Continue() 设置 jumpToCommandIndex 来指定要跳转到的下一条命令
                if (jumpToCommandIndex > -1)
                {
                    i = jumpToCommandIndex;
                    jumpToCommandIndex = -1;
                }

                // 跳过禁用的命令、注释和标签
                while (i < commandList.Count &&
                      (!commandList[i].enabled || 
                        commandList[i].GetType() == typeof(Comment) ||
                        commandList[i].GetType() == typeof(Label)))
                {
                    i = commandList[i].CommandIndex + 1;
                }

                if (i >= commandList.Count)
                {
                    break;
                }

                // 前一个活动命令用于 if / else / else if 命令逻辑
                if (activeCommand == null)
                {
                    previousActiveCommandIndex = -1;
                }
                else
                {
                    previousActiveCommandIndex = activeCommand.CommandIndex;
                }

                var command = commandList[i];
                activeCommand = command;

                if (flowchart.IsActive() && !suppressSelectionChanges)
                {
                    // 在特定情况下自动选中命令
                    if ((flowchart.SelectedCommands.Count == 0 && i == 0) ||
                        (flowchart.SelectedCommands.Count == 1 && flowchart.SelectedCommands[0].CommandIndex == previousActiveCommandIndex))
                    {
                        flowchart.ClearSelectedCommands();
                        flowchart.AddSelectedCommand(commandList[i]);
                    }
                }

                command.IsExecuting = true;
                // 该图标计时器由 FlowchartWindow 类管理，但此处也需设置——
                // 以防命令在窗口下次更新前就完成了执行
                command.ExecutingIconTimer = Time.realtimeSinceStartup + FungusConstants.ExecutingIconFadeTime;
                BlockSignals.DoCommandExecute(this, command, i, commandList.Count);

        #if UNITY_EDITOR
                try
                {
                    command.Execute();
                }
                catch (Exception)
                {
                    Debug.LogError("重新抛出异常，异常源自：" + command.GetLocationIdentifier());
                    throw;
                }
        #else
                command.Execute();
        #endif

                // 等待执行中的命令通过 Command.Continue() 设置要跳转到的下一条命令
                while (jumpToCommandIndex == -1)
                {
                    yield return null;
                }

                #if UNITY_EDITOR
                if (flowchart.StepPause > 0f)
                {
                    yield return new WaitForSeconds(flowchart.StepPause);
                }
                #endif

                command.IsExecuting = false;
            }

            if(State == ExecutionState.Executing &&
                // 确保不会因之前的终止操作而影响后续的执行实例
                executionCountAtStart == executionCount)
            {
                ReturnToIdle();
            }
        }

        private void ReturnToIdle()
        {
            executionState = ExecutionState.Idle;
            activeCommand = null;
            BlockSignals.DoBlockEnd(this);

            if (lastOnCompleteAction != null)
            {
                lastOnCompleteAction();
            }
            lastOnCompleteAction = null;
        }

        /// <summary>
        /// 停止执行该块中的命令。
        /// </summary>
        public virtual void Stop()
        {
            // 立即通知正在执行的命令停止
            if (activeCommand != null)
            {
                activeCommand.IsExecuting = false;
                activeCommand.OnStopExecuting();
            }

            // 这将导致执行循环在下一次迭代时终止
            jumpToCommandIndex = int.MaxValue;

            // 立即强制设为空闲状态，让依赖块执行状态的其他命令能在当前帧获取最新状态（而非下一帧）
            ReturnToIdle();
        }

        /// <summary>
        /// 返回所有与该块相连的块列表。
        /// </summary>
        public virtual List<Block> GetConnectedBlocks()
        {
            var connectedBlocks = new List<Block>();
            GetConnectedBlocks(ref connectedBlocks);
            return connectedBlocks;
        }

        public virtual void GetConnectedBlocks(ref List<Block> connectedBlocks)
        {
            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                if (command != null)
                {
                    command.GetConnectedBlocks(ref connectedBlocks);
                }
            }
        }

        /// <summary>
        /// 返回上一条正在执行的命令类型。
        /// </summary>
        /// <returns>上一条活动命令的类型</returns>
        public virtual System.Type GetPreviousActiveCommandType()
        {
            if (previousActiveCommandIndex >= 0 &&
                previousActiveCommandIndex < commandList.Count)
            {
                return commandList[previousActiveCommandIndex].GetType();
            }

            return null;
        }

        public virtual int GetPreviousActiveCommandIndent()
        {
            if (previousActiveCommandIndex >= 0 &&
                previousActiveCommandIndex < commandList.Count)
            {
                return commandList[previousActiveCommandIndex].IndentLevel;
            }

            return -1;
        }

        public virtual Command GetPreviousActiveCommand()
        {
            if (previousActiveCommandIndex >= 0 &&
                previousActiveCommandIndex < commandList.Count)
            {
                return commandList[previousActiveCommandIndex];
            }

            return null;
        }

        /// <summary>
        /// 重新计算列表中所有命令的缩进级别。
        /// </summary>
        public virtual void UpdateIndentLevels()
        {
            int indentLevel = 0;
            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                if (command == null)
                {
                    continue;
                }
                if (command.CloseBlock())
                {
                    indentLevel--;
                }
                // 缩进级别不允许为负数
                indentLevel = Math.Max(indentLevel, 0);
                command.IndentLevel = indentLevel;
                if (command.OpenBlock())
                {
                    indentLevel++;
                }
            }
        }

        /// <summary>
        /// 返回键值匹配的 Label 命令索引，未找到则返回 -1。
        /// </summary>
        public virtual int GetLabelIndex(string labelKey)
        {
            if (labelKey.Length == 0)
            {
                return -1;
            }

            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                var labelCommand = command as Label;
                if (labelCommand != null && String.Compare(labelCommand.Key, labelKey, true) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion
    }
}
