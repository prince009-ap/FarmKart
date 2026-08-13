import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FarmerAttendanceRecord, FarmerJob, FarmerJobApplication, FarmerJobRequest, FarmerWorkerAssignment, SaveJobAttendanceRequest } from '../../core/models/farmer.models';

@Injectable({ providedIn: 'root' })
export class FarmerJobService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/farmer/jobs`;
  private readonly appApiUrl = `${environment.apiUrl}/farmer/applications`;

  getMyJobs(): Observable<FarmerJob[]> { return this.http.get<FarmerJob[]>(this.apiUrl); }
  getJob(id: string): Observable<FarmerJob> { return this.http.get<FarmerJob>(`${this.apiUrl}/${id}`); }
  createJob(request: FarmerJobRequest): Observable<FarmerJob> { return this.http.post<FarmerJob>(this.apiUrl, request); }
  updateJob(id: string, request: FarmerJobRequest): Observable<FarmerJob> { return this.http.put<FarmerJob>(`${this.apiUrl}/${id}`, request); }
  deleteJob(id: string): Observable<void> { return this.http.delete<void>(`${this.apiUrl}/${id}`); }

  getJobApplications(jobId: string): Observable<FarmerJobApplication[]> {
    return this.http.get<FarmerJobApplication[]>(`${this.apiUrl}/${jobId}/applications`);
  }

  getApplicationDetails(applicationId: string): Observable<FarmerJobApplication> {
    return this.http.get<FarmerJobApplication>(`${this.appApiUrl}/${applicationId}`);
  }

  acceptApplication(applicationId: string): Observable<FarmerJobApplication> {
    return this.http.post<FarmerJobApplication>(`${this.appApiUrl}/${applicationId}/accept`, {});
  }

  rejectApplication(applicationId: string): Observable<FarmerJobApplication> {
    return this.http.post<FarmerJobApplication>(`${this.appApiUrl}/${applicationId}/reject`, {});
  }

  getJobAssignments(jobId: string): Observable<FarmerWorkerAssignment[]> {
    return this.http.get<FarmerWorkerAssignment[]>(`${this.apiUrl}/${jobId}/assignments`);
  }

  getJobAttendance(jobId: string, date?: string): Observable<FarmerAttendanceRecord[]> {
    const url = date ? `${this.apiUrl}/${jobId}/attendance?date=${date}` : `${this.apiUrl}/${jobId}/attendance`;
    return this.http.get<FarmerAttendanceRecord[]>(url);
  }

  saveJobAttendance(jobId: string, request: SaveJobAttendanceRequest): Observable<FarmerAttendanceRecord[]> {
    return this.http.post<FarmerAttendanceRecord[]>(`${this.apiUrl}/${jobId}/attendance`, request);
  }
}
