import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  ApplyJobRequest,
  UnreadNotificationCount,
  WorkerAssignment,
  WorkerAttendanceSummary,
  WorkerAvailableJob,
  WorkerEarningsSummary,
  WorkerJobApplication,
  WorkerNotification,
  WorkerPreferences,
  WorkerPreferencesUpdateRequest,
  WorkerProfile,
  WorkerProfileCompletion,
  WorkerProfileUpdateRequest,
  WorkerRatingSummary,
  WorkerWorkHistorySummary
} from '../../core/models/worker.models';

@Injectable({
  providedIn: 'root'
})
export class WorkerJobService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/worker`;

  getAvailableJobs(): Observable<WorkerAvailableJob[]> {
    return this.http.get<WorkerAvailableJob[]>(`${this.baseUrl}/jobs`, { withCredentials: true });
  }

  getJobDetails(id: string): Observable<WorkerAvailableJob> {
    return this.http.get<WorkerAvailableJob>(`${this.baseUrl}/jobs/${id}`, { withCredentials: true });
  }

  applyToJob(id: string, request?: ApplyJobRequest): Observable<WorkerJobApplication> {
    return this.http.post<WorkerJobApplication>(`${this.baseUrl}/jobs/${id}/apply`, request || {}, { withCredentials: true });
  }

  getMyApplications(): Observable<WorkerJobApplication[]> {
    return this.http.get<WorkerJobApplication[]>(`${this.baseUrl}/applications`, { withCredentials: true });
  }

  getMyAssignments(): Observable<WorkerAssignment[]> {
    return this.http.get<WorkerAssignment[]>(`${this.baseUrl}/assignments`, { withCredentials: true });
  }

  getAssignmentDetails(id: string): Observable<WorkerAssignment> {
    return this.http.get<WorkerAssignment>(`${this.baseUrl}/assignments/${id}`, { withCredentials: true });
  }

  getMyAttendance(): Observable<WorkerAttendanceSummary> {
    return this.http.get<WorkerAttendanceSummary>(`${this.baseUrl}/attendance`, { withCredentials: true });
  }

  getAssignmentAttendance(assignmentId: string): Observable<WorkerAttendanceSummary> {
    return this.http.get<WorkerAttendanceSummary>(`${this.baseUrl}/assignments/${assignmentId}/attendance`, { withCredentials: true });
  }

  getProfile(): Observable<WorkerProfile> {
    return this.http.get<WorkerProfile>(`${this.baseUrl}/profile`, { withCredentials: true });
  }

  updateProfile(request: WorkerProfileUpdateRequest): Observable<WorkerProfile> {
    return this.http.put<WorkerProfile>(`${this.baseUrl}/profile`, request, { withCredentials: true });
  }

  uploadProfileImage(file: File): Observable<WorkerProfile> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<WorkerProfile>(`${this.baseUrl}/profile/image`, formData, { withCredentials: true });
  }

  removeProfileImage(): Observable<WorkerProfile> {
    return this.http.delete<WorkerProfile>(`${this.baseUrl}/profile/image`, { withCredentials: true });
  }

  getProfileCompletion(): Observable<WorkerProfileCompletion> {
    return this.http.get<WorkerProfileCompletion>(`${this.baseUrl}/profile/completion`, { withCredentials: true });
  }

  getPreferences(): Observable<WorkerPreferences> {
    return this.http.get<WorkerPreferences>(`${this.baseUrl}/preferences`, { withCredentials: true });
  }

  updatePreferences(request: WorkerPreferencesUpdateRequest): Observable<WorkerPreferences> {
    return this.http.put<WorkerPreferences>(`${this.baseUrl}/preferences`, request, { withCredentials: true });
  }

  getNotifications(): Observable<WorkerNotification[]> {
    return this.http.get<any>(`${this.baseUrl}/notifications`, { withCredentials: true }).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res && Array.isArray(res.items)) return res.items;
        return [];
      })
    );
  }

  getUnreadNotificationCount(): Observable<UnreadNotificationCount> {
    return this.http.get<UnreadNotificationCount>(`${this.baseUrl}/notifications/unread-count`, { withCredentials: true });
  }

  markNotificationAsRead(id: string): Observable<WorkerNotification> {
    return this.http.put<WorkerNotification>(`${this.baseUrl}/notifications/${id}/read`, {}, { withCredentials: true });
  }

  markAllNotificationsAsRead(): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.baseUrl}/notifications/read-all`, {}, { withCredentials: true });
  }

  getReviews(): Observable<WorkerRatingSummary> {
    return this.http.get<WorkerRatingSummary>(`${this.baseUrl}/reviews`, { withCredentials: true });
  }

  getEarnings(): Observable<WorkerEarningsSummary> {
    return this.http.get<WorkerEarningsSummary>(`${this.baseUrl}/earnings`, { withCredentials: true });
  }

  getWorkHistory(): Observable<WorkerWorkHistorySummary> {
    return this.http.get<WorkerWorkHistorySummary>(`${this.baseUrl}/work-history`, { withCredentials: true });
  }
}
