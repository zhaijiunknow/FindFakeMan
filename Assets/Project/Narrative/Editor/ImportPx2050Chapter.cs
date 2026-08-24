using UnityEditor;
using UnityEngine;
using Project.Narrative.Scripts;

namespace Project.Narrative.Editor
{
    /// <summary>
    /// 把 PX-2050「序幕：失踪者」剧本文本导入为 VNChapterConfig 资产。
    /// 菜单：Tools/Project/Story/PX-2050 导入章节
    /// </summary>
    public static class ImportPx2050Chapter
    {
        private const string AssetPath = "Assets/Project/Narrative/Data/chapter_px2050.asset";

        private struct Line
        {
            public string speakerId;
            public string speakerName;
            public string text;

            public Line(string speakerId, string speakerName, string text)
            {
                this.speakerId = speakerId;
                this.speakerName = speakerName;
                this.text = text;
            }
        }

        private static readonly Line[] Lines =
        {
            // ===================== 序幕 =====================
            new("anchor", "主播",
                "本台最新通报：星途科技有限公司，自 2049 年 10 月至今，半年内连续发生7 起男性员工离奇失踪案。警方已成立专项组，但调查陷入停滞。"),
            new("anchor", "主播",
                "以下是失踪人员详细信息，如各位市民有相关线索请积极向警方通报。\n" +
                "张哲利，27 岁，算法工程师\n" +
                "李然，33 岁，区域销售主管\n" +
                "王浩，29 岁，行政后勤\n" +
                "陈默斯，31 岁，AI 训练师\n" +
                "赵峰，26 岁，硬件测试\n" +
                "孙章阳，34 岁，市场策划\n" +
                "周凯，28 岁，软件运维"),
            new("anchor", "主播",
                "7 名失踪者年龄集中 25—35 岁，遍布技术、研发、销售、行政、市场、运维全岗位。无财务问题、无家庭矛盾、无仇家、无出境记录。最后监控均为正常下班，随后彻底消失，车辆、证件、手机全部留在原地。"),
            new("anchor", "主播",
                "家属表示：他们性格正常，无异常言行，不像主动离家。"),
            new("anchor", "主播",
                "警方重申：未发现暴力、胁迫、自杀迹象，定性为高危失踪。"),
            new("narrator", "旁白",
                "（新闻画面突然出现 0.3 秒花屏音效，像被某种信号干扰）"),
            new("you", "你",
                "又是这种案子。没有挣扎、没有痕迹、没有理由。不是人类能做出来的。"),

            // ===================== 第一幕 =====================
            new("narrator", "旁白",
                "（耳机突然响起军用级加密电流声，无来电提示，直接接通）"),
            new("operator", "联络员",
                "A 先生，身份核验通过。案件编号：PX-2050-734。案件等级：高危伪人。管辖机构：守序者联盟第七行动组。"),
            new("you", "你",
                "星途科技 7 人失踪？"),
            new("operator", "联络员",
                "正确。常规警方已排查 14 天，结论：无人类作案可能。现场无 DNA、无指纹、无挣扎、无监控死角、无交通工具出入记录。"),
            new("narrator", "旁白",
                "【联盟绝密・案件核心细节】\n" +
                "一、7 名失踪者完整信息（警方隐藏版）\n" +
                "张哲｜27｜算法工程师｜最后出现：10 月 17 日 20:12 公司楼下｜异常：当天与白婉单独核对项目权限\n" +
                "李然｜33｜销售主管｜最后出现：11 月 3 日 19:40 地下车库｜异常：当天私下约白婉喝咖啡被拒\n" +
                "王浩｜29｜行政后勤｜最后出现：11 月 28 日 21:05 加班后｜异常：当天帮白婉搬运文件至仓库\n" +
                "陈默｜31｜AI 训练师｜最后出现：12 月 19 日 22:00 电梯口｜异常：当天向白婉提交加班申请\n" +
                "赵峰｜26｜硬件测试｜最后出现：1 月 5 日 18:30 下班打卡｜异常：当天向白婉领取办公设备\n" +
                "孙阳｜34｜市场策划｜最后出现：2 月 11 日 20:00 公司门口｜异常：当天与白婉对接活动物料\n" +
                "周凯｜28｜软件运维｜最后出现：3 月 23 日 21:30 机房门口｜异常：当天请白婉帮忙代签考勤"),
            new("narrator", "旁白",
                "二、警方无法解释的统一特征\n" +
                "全部为健康男性，25—35 岁，体态中等，无病史\n" +
                "失踪前 24 小时内，均与同一人发生直接接触\n" +
                "失踪后个人物品完全保留，像“人间蒸发”\n" +
                "无任何通讯告别、无转账、无行程、无消费\n" +
                "手机最后定位均停在一公司 500 米内\n" +
                "家属与同事均表示：失踪前情绪正常"),
            new("operator", "联络员",
                "人类凶手做不到统一干净、统一时间、统一目标类型。这可能是伪人狩猎模式。"),
            new("you", "你",
                "接触者是谁？"),
            new("operator", "联络员",
                "白婉。女，26 岁，半年前入职，行政专员。7 个人，唯一的交集。"),
            new("operator", "联络员",
                "今晚，执行任务，目标：调查疑似伪人出没地。"),
            new("you", "你",
                "收到……"),

            // ===================== 第二幕 =====================
            new("narrator", "旁白",
                "23:07，你驾驶无牌车辆抵达星途科技后门。破解门禁，进入空无一人的办公区。灯光半灭，冷气刺骨。"),
            new("operator", "联络员",
                "我已开放最高权限。你要查三样东西：\n" +
                "人事档案与背景核验\n" +
                "内部通讯、考勤、工作流\n" +
                "监控轨迹与接触记录"),
            new("narrator", "旁白",
                "第一：调取白婉档案・背景全是漏洞"),
            new("narrator", "旁白",
                "白婉・公开信息\n" +
                "姓名：白婉\n" +
                "性别：女\n" +
                "年龄：26 岁\n" +
                "职位：行政专员\n" +
                "入职时间：2049 年 10 月（第一个失踪者前 10 天入职）\n" +
                "形象：深褐卷发、红框眼镜、嘴角美人痣、固定黑白穿搭"),
            new("narrator", "旁白",
                "白婉・背景致命疑点（联盟核验结果）\n" +
                "身份证：号码有效，但户籍地址不存在，派出所无此人登记\n" +
                "学历：毕业院校查无学籍，学位证编号无效\n" +
                "上一家公司：为空壳公司，已注销\n" +
                "紧急联系人：空白\n" +
                "居住地址：仅填写“郊外别墅”，无房产登记\n" +
                "社保记录：入职前为零，像凭空出现\n" +
                "社交数据：无外卖、无快递、无网购、无打车、无通话记录\n" +
                "银行流水：仅工资入账，无任何消费"),
            new("operator", "联络员",
                "人类不会没有过去。她是模拟体，或被彻底控制的人。"),
            new("narrator", "旁白",
                "第二：锁定根据地"),
            new("operator", "联络员",
                "已通过信号三角定位，找到白婉的真实住址：郊外老旧独栋别墅。三公里内无住户、无监控、无路灯、无市政管线。"),
            new("you", "你",
                "她现在在哪？"),
            new("operator", "联络员",
                "不在别墅。你有40 分钟行动。"),
            new("you", "你",
                "别墅内有什么？"),
            new("operator", "联络员",
                "未知。但伪人常把狩猎场设在偏僻住所。失踪者……很可能在里面。"),
            new("narrator", "旁白",
                "第三：装备检查"),
            new("narrator", "旁白",
                "（你回到车内，打开后备厢。全息装备清单亮起）\n" +
                "【A 先生装备包】\n" +
                "伪造身份：星途科技技术顾问 A\n" +
                "1.收容箱：收容伪人物品，一共可以收容三个道具\n" +
                "2.工具包：破坏电子道具门锁以及撬开保险柜等道具，一共五点耐久\n" +
                "3.紫外线灯：可以显现伪人用特殊道具隐藏的物品或者笔迹\n" +
                "4.便携式探测器：可以探测出异常状态等级，以及可以还原部分原主人的异常行动场景，emp1 为非异常，2 级轻度疑似异常需要结合其他道具和其他物品判断（有三个其他异常则确定异常），3 级中度疑似异常（有两个其他异常则确定异常），4 级为高度疑似异常（有一个其他异常则确定异常），5 级为检测异常，会出现 cg 需要玩家根据 cg 效果判断。\n" +
                "5.温度计：探查温度。0 度以下为确定异常，0-10 度为重度疑似异常（有一个其他异常则确定异常）。10-20 度为中度疑似异常（有两个其他异常则确定异常），20—35 度为轻度疑似异常（有三个其他异常则确定异常），36—37 度无异常。\n" +
                "6.录音设备：可以录下该物品之前发生过事件的所有声音"),
            new("narrator", "旁白",
                "【联盟铁律】\n" +
                "不许暴露联盟\n" +
                "不许触发警报\n" +
                "不许与目标正面接触\n" +
                "探测器仅近距离有效，不可依赖\n" +
                "遇到无法解释的危险，立刻撤离"),
            new("operator", "联络员",
                "A 先生，记住。你在调查她的时候，她可能已经在狩猎你。"),
            new("narrator", "旁白",
                "车辆驶入林间小路，四周彻底黑暗。"),
            new("narrator", "旁白",
                "【系统提示】已抵达白婉别墅外围。潜入准备完成。")
        };

        [MenuItem("Tools/Project/Story/PX-2050 Import Chapter")]
        public static void Import()
        {
            var chapter = BuildChapter();
            if (AssetDatabase.LoadAssetAtPath<VNChapterConfig>(AssetPath) != null)
            {
                AssetDatabase.DeleteAsset(AssetPath);
            }

            AssetDatabase.CreateAsset(chapter, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = chapter;
            Debug.Log($"[PX-2050] 章节已导入: {AssetPath}（{Lines.Length} 句）");
        }

        private static VNChapterConfig BuildChapter()
        {
            var chapter = ScriptableObject.CreateInstance<VNChapterConfig>();

            // 普通 [Serializable] 类不能直接设 managedReferenceValue，改为逐子属性赋值。
            var serialized = new SerializedObject(chapter);
            serialized.FindProperty("chapterId").stringValue = "px2050_prologue";
            serialized.FindProperty("title").stringValue = "序幕：失踪者";
            serialized.FindProperty("startSequenceId").stringValue = "prologue";

            var sequencesProp = serialized.FindProperty("sequences");
            sequencesProp.arraySize = 1;
            var sequenceProp = sequencesProp.GetArrayElementAtIndex(0);
            sequenceProp.FindPropertyRelative("sequenceId").stringValue = "prologue";
            // requiredFlags / blockedFlags / nextSequenceId 保持默认空

            var nodesProp = sequenceProp.FindPropertyRelative("nodes");
            nodesProp.arraySize = Lines.Length;
            for (var i = 0; i < Lines.Length; i++)
            {
                var line = Lines[i];
                var nodeProp = nodesProp.GetArrayElementAtIndex(i);
                nodeProp.FindPropertyRelative("nodeId").stringValue = $"n{i + 1:D3}";
                nodeProp.FindPropertyRelative("speakerId").stringValue = line.speakerId;
                nodeProp.FindPropertyRelative("speakerName").stringValue = line.speakerName;
                nodeProp.FindPropertyRelative("text").stringValue = line.text;
                nodeProp.FindPropertyRelative("secondsPerCharacter").floatValue = 0.04f;
                // 其余字段（backgroundId/cgId/nextNodeId/choices 等）保持类默认值；
                // nextNodeId 留空，由列表顺序隐式推进。
            }

            serialized.FindProperty("endAction")
                .FindPropertyRelative("actionType").enumValueIndex = (int)VNEndActionType.ReturnToPreviousState;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return chapter;
        }
    }
}
