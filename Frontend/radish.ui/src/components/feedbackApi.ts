import { message as antdMessage, notification as antdNotification, type App } from 'antd';

type AntdAppApis = ReturnType<typeof App.useApp>;
type MessageApi = AntdAppApis['message'];
type NotificationApi = AntdAppApis['notification'];

let scopedMessageApi: MessageApi | null = null;
let scopedNotificationApi: NotificationApi | null = null;

function createFeedbackProxy<TApi extends object>(fallbackApi: TApi, resolveApi: () => TApi): TApi {
  return new Proxy(fallbackApi, {
    get(target, property, receiver) {
      const api = resolveApi();
      const value = Reflect.get(api, property, api === target ? receiver : api);
      return typeof value === 'function' ? value.bind(api) : value;
    },
  });
}

export const message = createFeedbackProxy(
  antdMessage as unknown as MessageApi,
  () => scopedMessageApi ?? (antdMessage as unknown as MessageApi)
);

export const notification = createFeedbackProxy(
  antdNotification as unknown as NotificationApi,
  () => scopedNotificationApi ?? (antdNotification as unknown as NotificationApi)
);

export function setScopedFeedbackApis(appApis: AntdAppApis) {
  scopedMessageApi = appApis.message;
  scopedNotificationApi = appApis.notification;
}

export function clearScopedFeedbackApis(appApis: AntdAppApis) {
  if (scopedMessageApi === appApis.message) {
    scopedMessageApi = null;
  }

  if (scopedNotificationApi === appApis.notification) {
    scopedNotificationApi = null;
  }
}
