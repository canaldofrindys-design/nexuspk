export type RateItem = { label: string; value: string };
export type LinkItem = { label: string; url: string };
export type ServerSettings = { name: string; tagline: string; about: string; rates: RateItem[]; communityLinks: LinkItem[] };
export type DownloadItem = { name: string; description: string | null; url: string; size: string | null; version: string | null };
export type DownloadSettings = { intro: string; requirements: string; items: DownloadItem[] };
export type PublicBanner = { id: number; title: string; subtitle: string | null; imageUrl: string | null; linkUrl: string | null; linkLabel: string | null };

export type SiteSnapshot = {
  server: ServerSettings;
  downloads: DownloadSettings;
  banners: PublicBanner[];
  serverUpdatedUtc: string | null;
  downloadsUpdatedUtc: string | null;
};

export type SettingView<T> = { value: T; updatedUtc: string | null; updatedBy: string | null };

export type BannerRecord = PublicBanner & {
  sortOrder: number;
  active: boolean;
  startsUtc: string | null;
  endsUtc: string | null;
  createdUtc: string;
  updatedUtc: string;
  createdBy: string | null;
  updatedBy: string | null;
};
