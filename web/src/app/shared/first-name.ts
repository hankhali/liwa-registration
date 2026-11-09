/** "Mariam Al Mansoori" → "Mariam"; "Dr. Ahmed Al Zaabi" → "Dr. Ahmed". Used for friendly greetings. */
export function greetingName(displayName: string): string {
  const parts = displayName.trim().split(/\s+/);
  if (parts.length === 0) return displayName;
  return /^dr\.?$/i.test(parts[0]) && parts.length > 1 ? `${parts[0]} ${parts[1]}` : parts[0];
}
