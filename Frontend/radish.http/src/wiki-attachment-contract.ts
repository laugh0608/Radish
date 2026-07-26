export const WikiAttachmentErrorCode = {
  InvalidReference: 'WikiAttachment.InvalidReference',
  ReferenceForbidden: 'WikiAttachment.ReferenceForbidden',
  CrossTenant: 'WikiAttachment.CrossTenant',
  TypeMismatch: 'WikiAttachment.TypeMismatch',
  ReferenceConflict: 'WikiAttachment.ReferenceConflict',
  SourceNotFound: 'WikiAttachment.SourceNotFound',
  AccessUnavailable: 'WikiAttachment.AccessUnavailable',
} as const;

export type WikiAttachmentErrorCodeValue =
  typeof WikiAttachmentErrorCode[keyof typeof WikiAttachmentErrorCode];
