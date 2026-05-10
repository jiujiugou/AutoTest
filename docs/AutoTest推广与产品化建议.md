# AutoTest 产品化建议书

> 从「一个人的工具」到「团队真正的依赖」——三条路径，八周可落地

---

## 一、当前状态诊断

### AutoTest 已有的

- 5 种监控类型全覆盖（HTTP / TCP / DB / Python / TEMPLATE DSL）
- 完整的调度引擎（Hangfire）+ 断言的执行管道
- 失败 → Outbox 持久化 → 钉钉通知 + AI 分析的闭环
- RBAC 权限 + JWT 认证
- `docker compose up -d` 一分钟部署
- 中等偏上的工程质量，分层架构清晰

### 阻止更多团队采用的三件事

1. **启动成本高**：部署好之后面对空面板，不知道第一个监控怎么建、建什么。5 种类型的选择本身就是认知负担。
2. **模板不可发现**：有人写好了一个"登录 → 查订单"模板，别人不知道、找不到、无法复用。
3. **文档散落**：架构、API、DSL 各一篇，但缺少一条从"我刚部署完"到"第一个监控在跑并且钉钉收到了通知"的直线。

---

## 二、三条核心建议

1. **建议一：一键导入** — 降低"第一个监控"从 30 分钟到 1 分钟。新用户部署完打开面板，按钮点一下就有一条监控在跑。
2. **建议二：监控模板库** — 把常见场景做成可复用的 JSON 模板，用户从下拉列表选场景 → 填参数 → 上线。
3. **建议三：五分钟上手文档** — 一条直线：部署 → 登录 → 导入模板 → 看钉钉通知。不需要读完全部设计文档。

---

## 三、建议一：一键导入（最优先）

### 问题

右面板空着，用户必须从零理解 HTTP/TCP/DB/Python/TEMPLATE 分别填什么、断言的 Field/Operator/Expected 怎么配、JSON 配置的字段名是什么。这 30 分钟里绝大多数人会放弃。

### 解决

在面板左侧任务列表加一个 **"导入"下拉按钮**，选场景 → 弹窗确认参数 → 直接创建好一个可运行的监控。

### 预置的一键导入场景

| 场景 | 类型 | 填什么 | 覆盖率 |
|------|------|--------|--------|
| 本地 API 健康检查 | HTTP | 输入 URL | 99% |
| Redis 端口探测 | TCP | host:port | 80% |
| MySQL 端口探测 | TCP | host:port | 80% |
| 登录 → 查用户信息 | TEMPLATE | API 地址 + 用户名/密码 | 70% |
| 证书过期检查 | Python | 域名 | 60% |
| 数据库行数检查 | DB | 连接串 + 表名 + 预期行数 | 50% |
| 外部服务可达 | TCP | 百度/微信/钉钉的 host:443 | 90% |
| 慢任务监控 | Python | 脚本路径 | 40% |

### 前端实现

```js
// Task.vue 新增
import { quickImportPresets } from './presets'

<el-dropdown @command="quickImport">
  <el-button>⚡ 一键导入</el-button>
  <el-dropdown-menu>
    <el-dropdown-item command="http_health">API 健康检查</el-dropdown-item>
    <el-dropdown-item command="redis_port">Redis 端口探测</el-dropdown-item>
    <el-dropdown-item command="mysql_port">MySQL 端口探测</el-dropdown-item>
    ...
  </el-dropdown-menu>
</el-dropdown>

function quickImport(presetKey) {
  const preset = presets[presetKey]
  // 弹窗让用户填 1-3 个参数（如 URL、host:port）
  // 填完后直接调用 POST /api/monitor 创建
  // 自动跳转到该监控的详情
}
```

### 预设数据格式

```js
// presets.js — 纯前端文件，不依赖后端
export const presets = {
  http_health: {
    label: 'API 健康检查',
    targetType: 'HTTP',
    fields: [
      { key: 'url', label: 'API 地址', placeholder: 'https://api.example.com/health' },
    ],
    build(params) {
      return {
        Name: `${params.url} 健康检查`,
        TargetType: 'HTTP',
        TargetConfig: JSON.stringify({
          Url: params.url, Method: 'Get', TimeoutSeconds: 10
        }),
        Assertions: [buildAssertion('HTTP', 'StatusCode', 'Equal', '200')],
        AutoDailyEnabled: true,
        AutoDailyTime: '09:00'
      }
    }
  },
  // ... 其他预设
}
```

---

## 四、建议二：监控模板库

### 问题

团队里张三写了一个"订单对账"的 TEMPLATE 模板写得很好，李四想要但不知道去哪找。模板散落在每个人的浏览器 localStorage 或脑子里。

### 解决

在 `examples/` 目录结构化，每个模板一个文件夹，配 README。前端加一个"模板市场"面板直接列出这些模板，点一下就导入。

### 模板目录结构

```
examples/
├── api-healthcheck/          ← 每个模板一个文件夹
│   ├── monitor.json          ← POST /api/monitor 的完整 DTO
│   ├── README.md             ← 中文说明：适用场景、参数说明、截图
│   └── screenshot.png        ← 执行成功的截图
├── redis-port-check/
├── mysql-port-check/
├── login-check-userinfo/     ← TEMPLATE DSL 示例
│   ├── monitor.json
│   ├── dsl-template.json     ← DSL 模板 JSON
│   └── README.md
├── cert-expiry-check/        ← Python 示例
├── db-rowcount-check/
├── dingtalk-external-reachable/
└── slow-task-monitor/
```

### 前端模板市场面板

```html
<!-- TemplateMarket.vue — 新页面 -->
<el-card v-for="tpl in templates" :key="tpl.id">
  <h3>{{ tpl.title }}</h3>
  <p>{{ tpl.description }}</p>
  <el-tag>{{ tpl.type }}</el-tag>
  <el-tag>{{ tpl.difficulty }}</el-tag>
  <el-button @click="importTemplate(tpl)">导入此模板</el-button>
</el-card>
```

**关键设计原则：**

- 模板是静态 JSON/JS 文件，随前端打包，不依赖后端 API。
- 模板 `monitor.json` 直接是 API 接受的 DTO，导入就是 `POST /api/monitor`。
- 参数化：模板中用 `%HOST%`、`%PORT%` 占位符，导入时弹窗让用户替换。

---

## 五、建议三：五分钟上手文档

### 当前文档问题

你写了 `架构详解`、`数据流详解`、`API 参考`、`DSL 模板引擎` 等，这是设计文档，不是新用户引导。新用户需要的不是理解 13 个项目之间的依赖关系，而是：

1. 部署起来
2. 看到第一个绿灯
3. 收到第一条钉钉通知
4. 知道下一步建什么

### 快速开始模板

```markdown
# AutoTest — 自动化监控平台

> 定时检测 HTTP/TCP/DB/业务流程，失败钉钉通知 + AI 诊断。一行命令部署。

## 快速开始

### 1. 部署（1 分钟）
cp .env.example .env
# 编辑 .env 填 AUTOTEST_JWT_SIGNING_KEY 和 SQL 密码
docker compose up -d

### 2. 登录并创建第一个监控（1 分钟）
打开 http://你的服务器IP
默认账号 admin / 你设的密码
点击"一键导入" → 选"API 健康检查" → 填你的 API 地址 → 确定

### 3. 收到钉钉通知（2 分钟）
编辑 docker-compose.yml 里 Outbox__Webhook__Url 改成你的钉钉机器人地址
手动执行一次刚才创建的监控
群里应该收到一张卡片

### 4. 下一步
浏览"模板市场"看更多监控模板
读 docs/ 了解 DSM 模板引擎（多步骤业务流程监控）

## 常见监控场景（点一下就导入）
| 我要 | 用 |
|------|----|
| API 掉线就通知 | HTTP 健康检查 |
| Redis/MySQL 不通就告警 | TCP 端口探测 |
| 订单数异常再通知 | DB 行数检查 |
| 登录→查订单→验金额一条链路 | TEMPLATE 模板 |
| 证书还有 7 天过期 | Python 证书检查 |
```

---

## 六、实施优先级与时间线

| 阶段 | 内容 |
|------|------|
| **第1周** | 预置 8 个一键导入场景、前端「导入」按钮、五分钟上手文档 |
| **第2周** | 模板市场页面、模板参数化（占位符）、运行截图 + README |
| **第3-4周** | 团队内部试用、收集问题、补充更多模板 |
| **第5-8周** | README 英文版、Docker Hub 镜像、发布到 V2EX/掘金 |

### P0（第一周必须做）

- 8 个一键导入预设（presets.js）
- Task.vue 的"导入"下拉按钮
- 根 README 改写为五分钟上手风格
- 4 个最常用模板的 monitor.json

### P1（第二周做完）

- TemplateMarket.vue 页面
- 占位符参数化 `<input>` 替换
- 每个模板的 README + 截图
- 文档里补 Docker Compose 故障排查

### P2（持续迭代）

- Docker Hub 发布镜像（免 git clone）
- 社区贡献模板的 PR 流程
- 钉钉群模板分享（导出/导入 JSON）
- 监控结果分享链接（"帮我看一下这个报错"）

---

## 七、为什么这些比"增强 AI"重要

**AI 解决的是"失败后怎么更快定位问题"——这是日活用户的需求。**

一键导入和模板市场解决的是 **"怎么让人先用起来"——这是获取日活用户的前提。**

你的 AI 已经够用了（失败时自动诊断 + 推送卡片）。现在的问题是：**知道这个工具的人太少，知道的人里因为"不知道第一个监控建什么"而放弃的太多。**

> 一键导入 + 模板库 → 日活增加 5 倍的价值 >> 把 AI 诊断准确率从 85% 提到 90% 的价值。

---

## 八、推广渠道建议

| 渠道 | 讲什么 | 目标 |
|------|--------|------|
| V2EX / 掘金 | "我花两个月写了一个自动化监控平台"——讲动机、讲坑、截图多放 | 获取前 100 个 Star |
| GitHub README | GIF 动图：部署 → 一键导入 → 钉钉通知（20 秒内展示完） | 访客 5 秒内理解项目做什么 |
| 钉钉开发者社区 | "用钉钉机器人做业务监控"——以钉钉用户视角切入 | 钉钉生态曝光 |
| Docker Hub | 发布镜像，README 写 "docker compose up" 一行启动 | 降低部署门槛 |
| 朋友圈/技术群 | "你们团队用什么做 API 监控？"——引发讨论而非硬推广 | 种子用户 |

---

## 九、一句话总结

> **不是多做功能，是把已有的磨到"一个陌生人 5 分钟就能跑出第一条通知"。**
>
> 做到这一点，就从一个"看着很厉害的项目"变成了一个"团队真的会用的工具"。

---

*AutoTest · 2026-05-10 · 实事求是*
