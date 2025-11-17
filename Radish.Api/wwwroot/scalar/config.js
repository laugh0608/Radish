// wwwroot/scalar/config.js
export default {
    // 自定义接口锚点生成规则，确保多语言标题也能稳定跳转
    generateOperationSlug: (operation) => `custom-${operation.method.toLowerCase()}${operation.path}`,
    // 监听文档切换事件，可在此注入埋点或提示
    onDocumentSelect: () => console.log('Document changed'),
    // 继续扩展 Scalar 支持的任何配置项
    // 参考：https://guides.scalar.com/scalar/scalar-api-references/configuration
}
