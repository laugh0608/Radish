/**
 * 快速入门指南数据
 */

export interface QuickStartStep {
  title: string;
  description: string;
  icon: string;
}

export interface QuickStartCategory {
  category: string;
  icon: string;
  steps: QuickStartStep[];
}

export const quickStartSteps: QuickStartCategory[] = [
  {
    category: '基础操作',
    icon: 'mdi:mouse',
    steps: [
      {
        title: '双击桌面图标打开应用',
        description: '在桌面上找到您想要使用的应用图标，双击即可打开',
        icon: 'mdi:cursor-default-click'
      },
      {
        title: '拖拽窗口标题栏移动窗口',
        description: '点击窗口顶部的标题栏并拖动，可以自由移动窗口位置',
        icon: 'mdi:drag'
      },
      {
        title: '点击红色按钮关闭窗口',
        description: '窗口左上角的红色圆点按钮用于关闭窗口',
        icon: 'mdi:close-circle'
      },
      {
        title: '顶部 Dock 显示运行中的应用',
        description: '屏幕顶部中间的 Dock 栏会显示所有正在运行的应用，点击可以切换或恢复窗口',
        icon: 'mdi:dock-bottom'
      }
    ]
  },
  {
    category: '论坛使用',
    icon: 'mdi:forum',
    steps: [
      {
        title: '浏览分类和帖子',
        description: '在论坛首页可以看到所有分类，点击分类查看该分类下的帖子列表',
        icon: 'mdi:view-list'
      },
      {
        title: '发布新帖子',
        description: '点击"发帖"按钮，填写标题和内容，支持 Markdown 格式和图片上传',
        icon: 'mdi:pencil'
      },
      {
        title: '评论和点赞',
        description: '在帖子详情页可以发表评论，也可以为喜欢的帖子和评论点赞',
        icon: 'mdi:heart'
      },
      {
        title: '查看个人主页',
        description: '点击用户头像可以查看个人主页，包括发布的帖子、评论等信息',
        icon: 'mdi:account'
      }
    ]
  },
  {
    category: '进阶功能',
    icon: 'mdi:star',
    steps: [
      {
        title: '访问管理控制台',
        description: '管理员用户可以访问控制台，进行用户管理、内容审核等操作',
        icon: 'mdi:console'
      },
      {
        title: '查看 API 文档',
        description: '开发者可以查看完整的 API 文档，了解接口使用方法',
        icon: 'mdi:book-open-page-variant'
      },
      {
        title: '自定义设置',
        description: '在设置中可以修改个人信息、偏好设置等（规划中）',
        icon: 'mdi:cog'
      }
    ]
  },
  {
    category: '获取帮助',
    icon: 'mdi:help-circle',
    steps: [
      {
        title: '查看文档中心',
        description: '访问文档中心了解详细的使用说明和技术文档',
        icon: 'mdi:book'
      },
      {
        title: '加入社区讨论',
        description: '在论坛中提问或参与讨论，与其他用户交流经验',
        icon: 'mdi:account-group'
      },
      {
        title: '提交问题反馈',
        description: '发现问题或有建议？欢迎在 GitHub 上提交 Issue',
        icon: 'mdi:bug'
      }
    ]
  }
];
