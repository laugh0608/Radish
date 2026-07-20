export const PAYMENT_PASSCODE_LENGTH = 6;
export const PAYMENT_PASSCODE_UPGRADE_REQUIRED_ERROR_CODE = 'PAYMENT_PASSCODE_UPGRADE_REQUIRED';

const repeatedDigitPattern = /^(\d)\1{5}$/;

function isStepSequence(value: string, step: 1 | -1): boolean {
  for (let index = 1; index < value.length; index += 1) {
    if (value.charCodeAt(index) - value.charCodeAt(index - 1) !== step) {
      return false;
    }
  }

  return true;
}

export function sanitizePaymentPasscode(value: string): string {
  return value.replace(/\D/g, '').slice(0, PAYMENT_PASSCODE_LENGTH);
}

export function isPaymentPasscodeFormatValid(value: string): boolean {
  return new RegExp(`^\\d{${PAYMENT_PASSCODE_LENGTH}}$`).test(value);
}

export function isRepeatedDigitPaymentPasscode(value: string): boolean {
  return repeatedDigitPattern.test(value);
}

export function isSequentialPaymentPasscode(value: string): boolean {
  if (!isPaymentPasscodeFormatValid(value)) {
    return false;
  }

  return isStepSequence(value, 1) || isStepSequence(value, -1);
}

export function isPaymentPasscodeValid(value: string): boolean {
  return isPaymentPasscodeFormatValid(value) && !isRepeatedDigitPaymentPasscode(value);
}

export function getPaymentPasscodeStrength(value: string): number {
  if (!value) {
    return 0;
  }

  if (!isPaymentPasscodeFormatValid(value)) {
    return 0;
  }

  if (isRepeatedDigitPaymentPasscode(value)) {
    return 1;
  }

  if (isSequentialPaymentPasscode(value)) {
    return 2;
  }

  const uniqueDigitCount = new Set(value).size;
  if (uniqueDigitCount <= 2) {
    return 3;
  }

  if (uniqueDigitCount === 3) {
    return 4;
  }

  return 5;
}

export function getPaymentPasscodeValidationMessage(
  value: string,
  options?: {
    requiredMessage?: string;
    formatMessage?: string;
    repeatedDigitsMessage?: string;
    allowRepeatedDigits?: boolean;
  }
): string | null {
  const trimmedValue = value.trim();
  if (!trimmedValue) {
    return options?.requiredMessage ?? 'Payment passcode is required';
  }

  if (!isPaymentPasscodeFormatValid(trimmedValue)) {
    return options?.formatMessage ?? `Payment passcode must contain ${PAYMENT_PASSCODE_LENGTH} digits`;
  }

  if (!options?.allowRepeatedDigits && isRepeatedDigitPaymentPasscode(trimmedValue)) {
    return options?.repeatedDigitsMessage ?? 'Payment passcode cannot repeat the same digit six times';
  }

  return null;
}

export function isPaymentPasscodeUpgradeRequiredError(
  error: { code?: string | null } | null | undefined
): boolean {
  if (!error) {
    return false;
  }

  return error.code === PAYMENT_PASSCODE_UPGRADE_REQUIRED_ERROR_CODE;
}
