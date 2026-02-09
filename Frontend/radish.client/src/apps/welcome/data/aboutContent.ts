/**
 * 关于 Radish 社区的内容数据
 */

export interface AboutSection {
  title: string;
  content: string;
  icon: string;
}

export interface Feature {
  name: string;
  description: string;
  icon: string;
  status?: 'available' | 'planned';
}

export interface TechHighlight {
  name: string;
  description: string;
  icon: string;
}

export const aboutContent = {
  vision: {
    title: '社区愿景',
    content: 'Radish 是一个现代化的社区平台，致力于为用户提供优质的交流和分享空间。我们相信开放、包容和创新的力量，通过技术连接每一个热爱分享的人。',
    icon: 'mdi:lightbulb-on'
  } as AboutSection,

  mission: {
    title: '我们的使命',
    content: '打造一个充满活力的社区生态，让每个人都能自由表达、学习成长、结识志同道合的朋友。我们追求卓越的用户体验，坚持开源精神，让技术服务于人。',
    icon: 'mdi:target'
  } as AboutSection,

  features: [
    {
      name: '论坛',
      description: '深度讨论与内容分享，支持 Markdown 编辑、图片上传、代码高亮等功能',
      icon: 'mdi:forum',
      status: 'available'
    },
    {
      name: '聊天室',
      description: '实时交流互动，支持文字、表情、图片等多种消息类型',
      icon: 'mdi:chat',
      status: 'planned'
    },
    {
      name: '萝卜币系统',
      description: '社区激励机制，通过发帖、评论、点赞等行为获得萝卜币奖励',
      icon: 'mdi:currency-usd',
      status: 'planned'
    },
    {
      name: '管理控制台',
      description: '强大的后台管理系统，支持用户管理、内容审核、数据统计等功能',
      icon: 'mdi:console',
      status: 'available'
    }
  ] as Feature[],

  techStack: [
    {
      name: '现代化技术栈',
      description: 'React 19 + ASP.NET Core 10 + PostgreSQL',
      icon: 'mdi:code-tags'
    },
    {
      name: '桌面化 UI 体验',
      description: '类似操作系统的 WebOS 界面，窗口化应用管理',
      icon: 'mdi:monitor'
    },
    {
      name: '多租户架构',
      description: '支持多租户隔离，灵活的权限控制和数据隔离',
      icon: 'mdi:office-building'
    },
    {
      name: '开源可自部署',
      description: '完全开源，支持私有化部署，数据完全掌控',
      icon: 'mdi:open-source-initiative'
    }
  ] as TechHighlight[]
};
