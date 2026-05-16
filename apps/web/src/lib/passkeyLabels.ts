// Source: https://github.com/passkeydeveloper/passkey-authenticator-aaguids
const AAGUID_NAMES: Record<string, string> = {
  "fbfc3007-154e-4ecc-8c0b-6e020557d7bd": "iCloud Keychain",
  "ea9b8d66-4d01-1d21-3c4a-4c03608595dc": "Google Password Manager",
  "adce0002-35bc-c60a-648b-0b25f1f05503": "Chrome on Mac",
  "08987058-cadc-4b81-b6e1-30de50dcbe96": "Windows Hello",
  "9ddd1817-af5a-4672-a2b9-3e3dd95000a9": "Windows Hello",
  "6028b017-b1d4-4c02-b4b3-afcdafc96bb2": "Windows Hello",
  "dd4ec289-e01d-41c9-bb89-70fa845d4bf2": "Windows Hello",
  "bada5566-a7aa-401f-bd96-45619a55a4b2": "1Password",
  "d548826e-79b4-db40-a3d8-11116f7e8349": "Bitwarden",
  "d1f52be5-88ad-4b9e-99e1-c0b7e317d7ab": "Dashlane",
  "2fc0579f-8113-47ea-b116-bb5a8db9202a": "YubiKey 5",
  "73bb0cd4-e502-49b8-9c6f-b59445bf720b": "YubiKey 5 FIPS",
  "c1f9a0bc-1dd2-404a-b27f-8e29047a43fd": "YubiKey 5 FIPS",
  "ee882879-721c-4913-9775-3dfcce97072a": "YubiKey 5",
};

export function getPasskeyLabel(
  aaguid: string | null,
  transports: string[],
  isBackupEligible: boolean,
): string {
  if (aaguid && AAGUID_NAMES[aaguid]) return AAGUID_NAMES[aaguid];
  if (isBackupEligible) return "Синхронизированный ключ";
  if (transports.some((t) => ["usb", "nfc", "ble", "smart-card"].includes(t)))
    return "Аппаратный ключ";
  if (transports.includes("hybrid")) return "Другое устройство";
  if (transports.includes("internal")) return "Этот браузер";
  return "Устройство";
}
