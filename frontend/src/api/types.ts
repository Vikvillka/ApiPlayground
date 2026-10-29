export type TracerResponse = {
  failure: string;
  statusCode: number | null;
  elapsedMilliseconds: number;
  headers: Record<string, string[]>;
  body: string;
  error: string | null;
  requestSent: boolean;
  historySaved?: boolean;
};

export type TracerRequest = {
  url: string;
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  headers: Record<string, string>;
  body?: string;
};

export type HistoryItem = {
  id: string;
  createdAt: string;
  method: TracerRequest["method"];
  url: string;
  redactedQueryParameters: string[];
  headers: { name: string; value: string; redacted: boolean }[];
  redactedHeaders: string[];
  body: string | null;
  failure: string;
  statusCode: number | null;
  elapsedMilliseconds: number;
};

export type SavedRequest = {
  id: string;
  createdAt: string;
  collectionId: string;
  collectionName: string;
  title: string;
  method: TracerRequest["method"];
  url: string;
  headers: { name: string; value: string; redacted: boolean }[];
  redactedHeaders: string[];
  body: string | null;
};

export type RequestCollection = {
  id: string;
  name: string;
  requests: SavedRequest[];
};

export type SaveRequestInput = {
  collectionId: string | null;
  newCollectionName: string | null;
  title: string;
  method: TracerRequest["method"];
  url: string;
  headers: Record<string, string>;
  body?: string;
};
