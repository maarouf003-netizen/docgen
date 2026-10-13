export type Role = 'lawyer' | 'head' | 'subhead' | 'manager' | 'admin' | 'entitymanager';

export interface UserDto {
  id: number;
  username: string;
  fullName: string;
  role: Role;
  branchId: number | null;
  branchName?: string | null;
  sectionId?: number | null;
  sectionName?: string | null;
}

export interface LoginResponse {
  user: UserDto;
}

export interface LoginBranchChoiceDto {
  branchId: number | null;
  branchName: string | null;
}

export interface LoginBranchSelectionResponse {
  requiresBranchSelection: true;
  branches: LoginBranchChoiceDto[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  perPage: number;
  totalCount: number;
  totalPages: number;
}
