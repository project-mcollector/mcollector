const COOKIE_UID_KEY = '_mc_uid';
const COOKIE_AID_KEY = '_mc_aid';
const COOKIE_TRAITS_KEY = '_mc_traits';

export class Storage {
  private cookieDomain?: string;

  constructor(cookieDomain?: string) {
    this.cookieDomain = cookieDomain;
  }

  public getAnonymousId(): string {
    let aid = this.get(COOKIE_AID_KEY);
    if (!aid) {
      aid = this.generateUUID();
      this.set(COOKIE_AID_KEY, aid, 365);
    }
    return aid;
  }

  public getUserId(): string | null {
    return this.get(COOKIE_UID_KEY);
  }

  public setUserId(userId: string): void {
    this.set(COOKIE_UID_KEY, userId, 365);
  }

  public clearUserId(): void {
    this.remove(COOKIE_UID_KEY);
  }

  public getTraits(): Record<string, any> {
    const traitsStr = this.get(COOKIE_TRAITS_KEY);
    if (traitsStr) {
      try {
        return JSON.parse(traitsStr);
      } catch (e) {
        return {};
      }
    }
    return {};
  }

  public setTraits(traits: Record<string, any>): void {
    this.set(COOKIE_TRAITS_KEY, JSON.stringify(traits), 365);
  }

  public clearTraits(): void {
    this.remove(COOKIE_TRAITS_KEY);
  }


  public get(key: string): string | null {
    const cookie = this.getCookie(key);
    if (cookie) return cookie;

    return this.getLocalStorage(key);
  }

  public set(key: string, value: string, days?: number): void {
    this.setCookie(key, value, days);
    this.setLocalStorage(key, value);
  }

  public remove(key: string): void {
    this.removeCookie(key);
    this.removeLocalStorage(key);
  }

  private setCookie(name: string, value: string, days?: number): void {
    try {
      let expires = '';
      if (days) {
        const date = new Date();
        date.setTime(date.getTime() + days * 24 * 60 * 60 * 1000);
        expires = '; expires=' + date.toUTCString();
      }
      const domain = this.cookieDomain ? `; domain=${this.cookieDomain}` : '';
      document.cookie = `${name}=${encodeURIComponent(value)}${expires}${domain}; path=/; SameSite=Lax`;
    } catch (e) {
    }
  }

  private getCookie(name: string): string | null {
    try {
      const nameEQ = `${name}=`;
      const ca = document.cookie.split(';');
      for (let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) === ' ') c = c.substring(1, c.length);
        if (c.indexOf(nameEQ) === 0) return decodeURIComponent(c.substring(nameEQ.length, c.length));
      }
    } catch (e) {}
    return null;
  }

  private removeCookie(name: string): void {
    this.setCookie(name, '', -1);
  }


  private setLocalStorage(key: string, value: string): void {
    try {
      window.localStorage.setItem(key, value);
    } catch (e) {}
  }

  private getLocalStorage(key: string): string | null {
    try {
      return window.localStorage.getItem(key);
    } catch (e) {}
    return null;
  }

  private removeLocalStorage(key: string): void {
    try {
      window.localStorage.removeItem(key);
    } catch (e) {}
  }


  private generateUUID(): string {
    if (typeof crypto !== 'undefined' && crypto.randomUUID) {
      return crypto.randomUUID();
    }

    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
      const r = (Math.random() * 16) | 0,
        v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }
}
