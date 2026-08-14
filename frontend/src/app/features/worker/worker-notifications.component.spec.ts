import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { WorkerNotificationsComponent } from './worker-notifications.component';
import { WorkerJobService } from './worker-job.service';
import { environment } from '../../../environments/environment';
import { WorkerNotification } from '../../core/models/worker.models';

describe('WorkerNotificationsComponent', () => {
  let component: WorkerNotificationsComponent;
  let fixture: ComponentFixture<WorkerNotificationsComponent>;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/worker/notifications`;

  const mockNotifs: WorkerNotification[] = [
    {
      id: 'n1',
      title: 'Application Accepted',
      message: 'Your application for Harvesting Job was accepted.',
      notificationType: 'Application',
      isRead: false,
      createdAtUtc: '2026-08-14T10:00:00Z'
    },
    {
      id: 'n2',
      title: 'Assignment Created',
      message: 'You have been assigned to Harvesting Job.',
      notificationType: 'Assignment',
      isRead: true,
      createdAtUtc: '2026-08-14T10:05:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkerNotificationsComponent, NoopAnimationsModule],
      providers: [
        WorkerJobService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'worker/applications', component: WorkerNotificationsComponent },
          { path: 'worker/assignments', component: WorkerNotificationsComponent },
          { path: 'worker/attendance', component: WorkerNotificationsComponent },
          { path: 'worker/jobs', component: WorkerNotificationsComponent }
        ])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerNotificationsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create component', () => {
    expect(component).toBeTruthy();
  });

  it('should load notifications and calculate unread count', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush(mockNotifs);

    expect(component.loading()).toBe(false);
    expect(component.notifications().length).toBe(2);
    expect(component.unreadCount()).toBe(1);
  });

  it('should handle load error', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(baseUrl);
    req.flush({ message: 'Error' }, { status: 500, statusText: 'Server Error' });

    expect(component.loading()).toBe(false);
    expect(component.loadError()).toBeTruthy();
  });

  it('should mark single notification as read', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockNotifs);

    const unreadNotif = mockNotifs[0];
    component.markAsRead(unreadNotif);

    const putReq = httpMock.expectOne(`${baseUrl}/n1/read`);
    expect(putReq.request.method).toBe('PUT');
    putReq.flush({ ...unreadNotif, isRead: true });

    expect(component.unreadCount()).toBe(0);
    expect(component.notifications()[0].isRead).toBe(true);
  });

  it('should mark all notifications as read', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockNotifs);

    component.markAllAsRead();

    const putReq = httpMock.expectOne(`${baseUrl}/read-all`);
    expect(putReq.request.method).toBe('PUT');
    putReq.flush({ message: 'All marked read' });

    expect(component.unreadCount()).toBe(0);
    expect(component.notifications().every(n => n.isRead)).toBe(true);
  });

  it('should map icons accurately for notification titles', () => {
    expect(component.getNotificationIcon('Application', 'Application Accepted')).toBe('check_circle');
    expect(component.getNotificationIcon('Application', 'Application Rejected')).toBe('cancel');
    expect(component.getNotificationIcon('Assignment', 'Assignment Created')).toBe('assignment_ind');
    expect(component.getNotificationIcon('Job', 'Attendance Updated')).toBe('event_available');
    expect(component.getNotificationIcon('Job', 'Job Cancelled')).toBe('event_busy');
  });

  it('should not call markAsRead API if notification is already read', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockNotifs);

    const readNotif = mockNotifs[1];
    component.markAsRead(readNotif);

    httpMock.expectNone(`${baseUrl}/n2/read`);
  });

  it('should navigate on notification click', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockNotifs);

    component.navigateToNotificationTarget(mockNotifs[0]);
    const putReq = httpMock.expectOne(`${baseUrl}/n1/read`);
    putReq.flush({ ...mockNotifs[0], isRead: true });
  });
});
