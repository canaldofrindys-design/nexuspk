export type EventState = "scheduled" | "live" | "ended" | "cancelled";

export type NewsSummary = {
  slug: string;
  category: string;
  title: string;
  summary: string;
  imageUrl: string | null;
  pinned: boolean;
  publishedUtc: string;
};

export type NewsArticle = Omit<NewsSummary, "pinned"> & { body: string; authorName: string | null; updatedUtc: string };

export type NewsPage = { items: NewsSummary[]; total: number; page: number; pageSize: number };

export type EventSummary = {
  slug: string;
  category: string;
  name: string;
  summary: string;
  imageUrl: string | null;
  location: string | null;
  startsUtc: string;
  endsUtc: string | null;
  state: EventState;
};

export type EventDetail = {
  event: EventSummary;
  description: string;
  rules: string | null;
  prizes: string | null;
  executionMode: string;
  updatedUtc: string;
  reminderSet: boolean | null;
};

export type EventOverview = { live: EventSummary[]; upcoming: EventSummary[]; recent: EventSummary[]; generatedUtc: string };

export type EventCalendar = { year: number; month: number; events: EventSummary[] };
