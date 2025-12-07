export interface ApiResponse<T> {
    statusCode: number;
    isSuccess: boolean;
    messageInfo: string;
    messageInfoDev?: string;
    code?: string;
    messageKey?: string;
    responseData?: T;
}

export function parseApiResponse<T>(json: ApiResponse<T>, t: (key: string) => string) {
    let message = json.messageInfo;

    if (json.messageKey) {
        const localized = t(json.messageKey);
        if (localized && localized !== json.messageKey) {
            message = localized;
        }
    }

    return {
        ok: json.isSuccess,
        data: json.isSuccess ? json.responseData : undefined,
        message,
        code: json.code,
    } as const;
}
