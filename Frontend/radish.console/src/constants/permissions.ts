export const CONSOLE_PERMISSIONS = {
  dashboardView: 'console.dashboard.view',
  applicationsView: 'console.applications.view',
  productsView: 'console.products.view',
  ordersView: 'console.orders.view',
  usersView: 'console.users.view',
  rolesView: 'console.roles.view',
  rolesCreate: 'console.roles.create',
  rolesEdit: 'console.roles.edit',
  rolesToggle: 'console.roles.toggle',
  rolesDelete: 'console.roles.delete',
  tagsView: 'console.tags.view',
  stickersView: 'console.stickers.view',
  systemConfigView: 'console.system-config.view',
  hangfireView: 'console.hangfire.view',
} as const;

export type ConsolePermission = typeof CONSOLE_PERMISSIONS[keyof typeof CONSOLE_PERMISSIONS];
