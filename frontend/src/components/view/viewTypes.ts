export type PersonFields = {
  name?: string;
  father?: string;
  family?: string;
  mother?: string;
  birth?: string;
  register?: string;
  nationalId?: string;
  addressType?: string;
  address?: string;
};

export type HeirLine = { name: string; detail: string };

export type DetailsRow = { label: string; value: string };

export type PartyModal =
  | { kind: 'person'; title: string; rows: DetailsRow[] }
  | { kind: 'entity'; name: string; branch: string; governorate?: string }
  | { kind: 'heirs'; deceasedName: string; lines: HeirLine[] };
