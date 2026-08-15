export type PeriodSelection = { year: number; month?: number; quarter?: number };

export type PeriodOption = PeriodSelection & { value: string; label: string };
