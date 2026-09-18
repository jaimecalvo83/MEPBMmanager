
export interface PlayerInfo {
  id: string;
  userId: string;
  username: string;
  email: string;
  isReady: boolean;
  acceptedAt: string | null;
  wantsToPlayWith: { userId: string; username: string } | null;
}


export interface AdminInfo {
  id: string;
  userId: string;
  username: string;
  email: string;
  isReady: boolean;
  acceptedAt: string | null;
}


export type TabType = 'nation' | 'map' | 'cities' | 'armies' | 'characters' | 'orders' | 'messages' | 'relations' | 'reports' | 'standings';
