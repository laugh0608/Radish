/**
 * 欢迎页社区约定数据
 */

export interface Rule {
  title: string;
  description: string;
  severity?: 'high' | 'medium' | 'low';
}

export interface RuleCategory {
  category: string;
  icon: string;
  rules: Rule[];
}

export const communityRules: RuleCategory[] = [
  {
    category: '内容发布',
    icon: 'mdi:file-document-edit-outline',
    rules: [
      {
        title: '标题清楚，内容对题',
        description: '发帖、提问和文档内容应尽量说明背景、问题和目标，避免只留下模糊标题或无上下文内容。',
        severity: 'medium'
      },
      {
        title: '禁止违法、侵权与恶意广告',
        description: '涉及违法违规、侵犯他人权益、批量引流、诈骗或恶意推广的内容会被优先处理。',
        severity: 'high'
      },
      {
        title: '引用资料时保留必要来源',
        description: '转载、节选或引用他人资料时请说明出处，避免把外部内容伪装成原创成果。',
        severity: 'medium'
      }
    ]
  },
  {
    category: '互动边界',
    icon: 'mdi:account-group-outline',
    rules: [
      {
        title: '先讨论问题，再评价个人',
        description: '鼓励观点碰撞，但不要把讨论变成人身攻击、嘲讽、贴标签或故意挑衅。',
        severity: 'high'
      },
      {
        title: '不同意见请给出依据',
        description: '如果你不同意某个方案，优先补充事实、例子或替代做法，而不是只做情绪化否定。',
        severity: 'medium'
      },
      {
        title: '善用点赞、回复与举报',
        description: '鼓励通过正常互动表达反馈；发现明显违规内容时，优先使用举报而不是继续扩大争吵。',
        severity: 'low'
      }
    ]
  },
  {
    category: '账号与数据安全',
    icon: 'mdi:shield-account-outline',
    rules: [
      {
        title: '不要共享账号与敏感信息',
        description: '请妥善保护密码、令牌和个人资料，不要在公开内容里泄露他人的联系方式或隐私信息。',
        severity: 'high'
      },
      {
        title: '禁止刷赞、刷评论和恶意养号',
        description: '不得通过批量账号、脚本或组织化操作影响内容排序、互动数据和奖励判断。',
        severity: 'high'
      },
      {
        title: '联调阶段更要注意边界',
        description: '当前仍处在持续联调阶段，请避免使用非常规方式压测、探测或绕过既有权限边界。',
        severity: 'medium'
      }
    ]
  },
  {
    category: '反馈与协作',
    icon: 'mdi:message-draw',
    rules: [
      {
        title: '反馈问题尽量带上下文',
        description: '描述问题时建议附上页面、入口、复现步骤和预期结果，便于快速定位。',
        severity: 'low'
      },
      {
        title: '欢迎指出文案与体验不一致',
        description: '欢迎页、Docs 和实际功能如果出现偏差，请直接指出，我们会优先统一口径。',
        severity: 'low'
      },
      {
        title: '阶段性能力以当前实现为准',
        description: '欢迎页不会承诺尚未落地的功能，联调阶段如有变化，请以当前桌面里实际可见能力为准。',
        severity: 'medium'
      }
    ]
  }
];
