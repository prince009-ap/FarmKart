import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { NotificationResponse, UnreadCountResponse, NotificationQueryRequest, PagedNotificationResponse } from '../models/notification.models';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/notifications`;

  private readonly unreadCountSubject = new BehaviorSubject<number>(0);
  public readonly unreadCount$ = this.unreadCountSubject.asObservable();

  getPagedNotifications(params?: NotificationQueryRequest): Observable<PagedNotificationResponse> {
    let httpParams = new HttpParams();
    if (params?.filter) httpParams = httpParams.set('filter', params.filter);
    if (params?.category) httpParams = httpParams.set('category', params.category);
    if (params?.search) httpParams = httpParams.set('search', params.search);
    if (params?.page) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<PagedNotificationResponse>(this.apiUrl, { params: httpParams }).pipe(
      tap(res => this.unreadCountSubject.next(res.unreadCount))
    );
  }

  getNotifications(): Observable<NotificationResponse[]> {
    return this.http.get<any>(this.apiUrl).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res && Array.isArray(res.items)) return res.items;
        return [];
      })
    );
  }

  getUnreadCount(): Observable<UnreadCountResponse> {
    return this.http.get<UnreadCountResponse>(`${this.apiUrl}/unread-count`).pipe(
      tap(res => this.unreadCountSubject.next(res.unreadCount))
    );
  }

  refreshUnreadCount(): void {
    this.getUnreadCount().subscribe();
  }

  markAsRead(notificationId: string): Observable<NotificationResponse> {
    return this.http.patch<NotificationResponse>(`${this.apiUrl}/${notificationId}/read`, {}).pipe(
      tap(() => this.refreshUnreadCount())
    );
  }

  markAllAsRead(): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/read-all`, {}).pipe(
      tap(() => this.unreadCountSubject.next(0))
    );
  }

  deleteNotification(notificationId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${notificationId}`).pipe(
      tap(() => this.refreshUnreadCount())
    );
  }

  clearAllNotifications(): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/clear-all`).pipe(
      tap(() => this.unreadCountSubject.next(0))
    );
  }
}
