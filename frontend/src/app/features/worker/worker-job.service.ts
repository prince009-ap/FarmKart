import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApplyJobRequest, WorkerAssignment, WorkerAttendanceSummary, WorkerAvailableJob, WorkerJobApplication, WorkerProfile, WorkerProfileUpdateRequest } from '../../core/models/worker.models';

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
}
