export interface UserAdornmentItem {
  voResourceKey: string;
  voName: string;
  voImageUrl?: string | null;
}

export interface UserAdornment {
  voBadge?: UserAdornmentItem | null;
  voTitle?: UserAdornmentItem | null;
}
