import { useRef } from 'react';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { PrivacySafetyBoundaryPanel } from '../../privacy/PrivacySafetyBoundaryPanel';
import styles from './PublicCommitmentsApp.module.css';

interface CommitmentSection {
  id: string;
  eyebrow: string;
  title: string;
  description: string;
  items: string[];
}

const commitmentSections: CommitmentSection[] = [
  {
    id: 'community',
    eyebrow: 'Community Rules',
    title: '社区内容与互动公约',
    description: 'Radish 的核心是帖子、评论、聊天和关系沉淀。所有公开内容与互动都应服务真实讨论、知识沉淀和互相帮助。',
    items: [
      '禁止发布违法违规、侵权、恶意广告、欺诈误导、色情低俗、人身攻击、骚扰或恶意引战内容。',
      '发帖、评论、回答和聊天应围绕主题展开；引用外部资料时应保留必要来源，不冒充他人或伪造事实。',
      '点赞、轻回应、关注和举报用于表达参与和维护秩序，禁止刷赞、刷评论、恶意举报和批量养号。',
      '用户可以对帖子、评论、轻回应、聊天消息和商品提交举报；治理处理会优先保留目标快照、原因和处理记录。',
    ],
  },
  {
    id: 'privacy',
    eyebrow: 'Privacy Boundary',
    title: '公开、私域与 Console 可见边界',
    description: '正式版需要让用户知道哪些信息会公开，哪些仅用于本人复访或后台治理。',
    items: [
      '公开主页、公开帖子、公开评论、公开榜单和公开商品会被未登录访客查看；账号凭证、资产明细、订单、附件管理和私域状态不作为公开内容展示。',
      '关注关系、经验、宠物、背包和资产信息只在对应页面按当前产品边界展示；若后续扩大公开范围，应先更新说明和验证口径。',
      'Console 仅面向授权管理员，用于用户排障、内容治理、订单 / 权益核对、权限和系统设置管理；高风险动作需要权限、确认和审计留痕。',
      '请勿在公开内容或聊天中发布他人的联系方式、账号凭证、订单信息、资产流水、未授权截图或其他隐私资料。',
    ],
  },
  {
    id: 'terms',
    eyebrow: 'User Terms',
    title: '账号、安全与使用承诺',
    description: '用户应对自己的账号和操作负责，平台会优先维护社区信任、资产安全和治理可追踪性。',
    items: [
      '用户应妥善保管账号、密码、支付口令和登录会话，不共享账号，不绕过权限或滥用接口。',
      '发布、编辑、购买、关注、举报、治理和资产相关动作会按业务需要记录时间、目标对象、操作者和处理结果。',
      '发现账号异常、购买失败、权益未到账、内容被误处理或权限异常时，应通过反馈或举报入口提供时间、路径、目标对象和诊断信息。',
      '平台可以对明显违规、攻击、刷屏、欺诈、骚扰或破坏社区秩序的账号执行禁言、封禁、撤销处理或其他必要限制。',
    ],
  },
  {
    id: 'virtual-assets',
    eyebrow: 'Virtual Goods',
    title: '虚拟商品、资产与权益边界',
    description: '商城、资产、背包、经验和宠物服务于社区贡献、身份表达和复访反馈，不等同于现金账户或金融产品。',
    items: [
      'Radish 内的胡萝卜、经验、背包权益、宠物状态和虚拟商品没有现金价值，不提供提现、转售或线下兑换承诺。',
      '当前商城以虚拟商品和权益发放为主；购买前应确认商品名称、价格、库存、可购买状态和到账路径。',
      '当前正式边界不承诺完整退款 / 售后平台；因系统错误造成的重复扣减、发放异常或订单失败，应按订单、资产流水和背包记录核对处理。',
      '任何资产、订单、背包或权益异常都应能关联订单号、业务目标、用户和时间点，避免仅凭截图或口头描述处理。',
    ],
  },
  {
    id: 'minors',
    eyebrow: 'Sensitive Use',
    title: '未成年人、敏感内容与安全反馈',
    description: 'Radish 不鼓励未成年人在无监护人理解的情况下参与交易或公开发布敏感信息。',
    items: [
      '未成年人应在监护人知情和指导下使用社区互动、聊天、商城和资产相关能力。',
      '不得发布自伤、暴力、仇恨、色情、赌博、毒品、诈骗、隐私泄露或其他可能伤害他人的内容。',
      '遇到骚扰、威胁、欺诈、侵权、隐私泄露或疑似未成年人风险时，应优先使用举报入口并保留上下文。',
      '涉及人身安全、违法犯罪或紧急风险时，应优先联系现实中的可信成年人、平台管理员或当地相关机构。',
    ],
  },
  {
    id: 'support',
    eyebrow: 'Support Evidence',
    title: '反馈、诊断与问题恢复',
    description: '正式问题反馈需要能帮助管理员定位用户、路径、时间和目标对象。',
    items: [
      '反馈页面异常时，请尽量提供当前路径、操作时间、诊断编号、截图和复现步骤。',
      '反馈内容异常时，请提供帖子、评论、聊天消息、商品、订单、用户或举报单编号。',
      '购买、背包、资产、经验或宠物状态异常时，请提供订单、流水、背包来源或相关页面路径。',
      'Console 管理员处理治理问题时，应优先基于目标快照、举报原因、用户历史和动作日志形成判断。',
    ],
  },
];

export function PublicCommitmentsApp() {
  const pageRef = useRef<HTMLDivElement | null>(null);

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="规"
        brandName="Radish 承诺"
        brandSubline="Community Trust"
        activeKey="legal"
        onBrandClick={() => pageRef.current?.scrollTo({ top: 0, behavior: 'smooth' })}
        loginLabel="登录"
      />

      <main className={styles.main}>
        <section className={styles.hero}>
          <span className={styles.eyebrow}>Radish Community Commitments</span>
          <h1>正式用户承诺与社区边界</h1>
          <p>
            本页面向所有用户说明 Radish 当前正式产品边界：社区内容如何互动，公开与私域信息如何区分，
            虚拟商品和资产如何理解，以及遇到问题时应如何提供可追踪证据。
          </p>
          <div className={styles.anchorRail} aria-label="承诺章节">
            {commitmentSections.map((section) => (
              <a key={section.id} href={`#${section.id}`}>
                {section.title}
              </a>
            ))}
          </div>
        </section>

        <section className={styles.summaryGrid} aria-label="发布边界摘要">
          <div>
            <span>核心主线</span>
            <strong>帖子、聊天、关系</strong>
            <p>Docs、商城、资产、宠物、经验、通知和 Workbench 都服务社区内容生产、互动和复访。</p>
          </div>
          <div>
            <span>信任来源</span>
            <strong>举报、快照、审计</strong>
            <p>治理判断应可回看目标、上下文、处理理由和动作日志。</p>
          </div>
          <div>
            <span>资产边界</span>
            <strong>虚拟权益，不等同现金</strong>
            <p>虚拟商品、胡萝卜、经验和背包权益不提供现金价值承诺。</p>
          </div>
        </section>

        <PrivacySafetyBoundaryPanel />

        <div className={styles.sectionList}>
          {commitmentSections.map((section) => (
            <section key={section.id} id={section.id} className={styles.section}>
              <div className={styles.sectionHeader}>
                <span>{section.eyebrow}</span>
                <h2>{section.title}</h2>
                <p>{section.description}</p>
              </div>
              <ul>
                {section.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      </main>
    </div>
  );
}
