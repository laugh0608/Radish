/**
 * 社区规则数据
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
    category: '基本准则',
    icon: 'mdi:shield-check',
    rules: [
      {
        title: '尊重他人，文明交流',
        description: '保持礼貌和尊重，不进行人身攻击、侮辱、歧视等行为',
        severity: 'high'
      },
      {
        title: '禁止发布违法违规内容',
        description: '不得发布违反法律法规的内容，包括但不限于暴力、色情、赌博、毒品等',
        severity: 'high'
      },
      {
        title: '保护个人隐私',
        description: '不得未经许可公开他人的个人信息，包括真实姓名、联系方式、地址等',
        severity: 'high'
      },
      {
        title: '禁止恶意灌水和广告',
        description: '不得发布无意义的灌水内容或未经许可的商业广告',
        severity: 'medium'
      }
    ]
  },
  {
    category: '论坛规则',
    icon: 'mdi:forum',
    rules: [
      {
        title: '发帖规范',
        description: '标题应简洁明了，内容应充实有价值，选择合适的分类',
        severity: 'medium'
      },
      {
        title: '评论规范',
        description: '评论应与主题相关，理性讨论，避免无意义的争吵',
        severity: 'medium'
      },
      {
        title: '点赞和举报机制',
        description: '合理使用点赞功能，发现违规内容请及时举报',
        severity: 'low'
      },
      {
        title: '禁止刷赞刷评论',
        description: '不得通过机器或人工方式恶意刷赞、刷评论',
        severity: 'high'
      }
    ]
  },
  {
    category: '账号管理',
    icon: 'mdi:account-cog',
    rules: [
      {
        title: '账号安全建议',
        description: '使用强密码，定期更换密码，不要与他人共享账号',
        severity: 'medium'
      },
      {
        title: '多账号使用规则',
        description: '允许拥有多个账号，但不得用于恶意行为（如刷赞、自问自答等）',
        severity: 'medium'
      },
      {
        title: '违规处理流程',
        description: '违规行为将根据严重程度进行警告、禁言、封号等处理',
        severity: 'high'
      }
    ]
  },
  {
    category: '萝卜币规则',
    icon: 'mdi:currency-usd',
    rules: [
      {
        title: '获取方式',
        description: '通过发帖、评论、点赞等正常社区行为获得萝卜币奖励（规划中）',
        severity: 'low'
      },
      {
        title: '使用规则',
        description: '萝卜币可用于打赏、兑换特权等功能（规划中）',
        severity: 'low'
      },
      {
        title: '禁止交易',
        description: '严禁私下买卖萝卜币或进行任何形式的现金交易',
        severity: 'high'
      }
    ]
  },
  {
    category: '重要公告',
    icon: 'mdi:bullhorn',
    rules: [
      {
        title: '系统维护通知',
        description: '系统维护期间可能无法访问，请关注公告了解维护时间',
        severity: 'low'
      },
      {
        title: '功能更新说明',
        description: '新功能上线或重大更新时会发布公告，请及时查看',
        severity: 'low'
      },
      {
        title: '社区活动',
        description: '定期举办社区活动，欢迎积极参与',
        severity: 'low'
      }
    ]
  }
];
