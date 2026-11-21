export type PersonFields = {
  name?: string | null;
  father?: string | null;
  family?: string | null;
  mother?: string | null;
  birth?: string | null;
  register?: string | null;
  nationalId?: string | null;
  addressType?: string | null;
  address?: string | null;
};

export type HeirLine = { name: string; detail: string };

export type DetailsRow = { label: string; value: string };

export type PartyModal =
  | { kind: 'person'; title: string; rows: DetailsRow[] }
  | { kind: 'entity'; name: string; branch: string; governorate?: string }
  | { kind: 'heirs'; deceasedName: string; lines: HeirLine[] };
