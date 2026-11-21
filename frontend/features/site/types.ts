export type RateItem = { label: string; value: string };
export type LinkItem = { label: string; url: string };
export type ServerSettings = { name: string; tagline: string; about: string; rates: RateItem[]; communityLinks: LinkItem[] };
export type DownloadItem = { name: string; description: string | null; url: string; size: string | null; version: string | null };
export type DownloadSettings = { intro: string; requirements: string; items: DownloadItem[] };
export type PublicBanner = { id: number; title: string; subtitle: string | null; imageUrl: string | null; linkUrl: string | null; linkLabel: string | null };

/** Drop raro anunciado pela equipe ("Drops raros da semana" da home). */
export type RareDrop = { item: string; icon: string; boss: string | null; player: string; droppedUtc: string };
export type RareDropSettings = { items: RareDrop[] };
export type ItemPickupStat = { itemCode: number; name?: string; boss?: string | null; times: number; lastUtc: string; lastCharacter: string };
export type ItemPickups = { available: boolean; exactNames?: number; dropSources?: number; items: ItemPickupStat[] };

export type SiteSnapshot = {
  server: ServerSettings;
  downloads: DownloadSettings;
  banners: PublicBanner[];
  serverUpdatedUtc: string | null;
  downloadsUpdatedUtc: string | null;
  rareDrops?: RareDrop[] | null;
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
