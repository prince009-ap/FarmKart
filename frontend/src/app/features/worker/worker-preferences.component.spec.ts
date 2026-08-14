import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { WorkerPreferencesComponent } from './worker-preferences.component';
import { WorkerJobService } from './worker-job.service';
import { environment } from '../../../environments/environment';
import { WorkerPreferences } from '../../core/models/worker.models';

describe('WorkerPreferencesComponent', () => {
  let component: WorkerPreferencesComponent;
  let fixture: ComponentFixture<WorkerPreferencesComponent>;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/worker/preferences`;

  const mockPreferences: WorkerPreferences = {
    preferredWorkCategories: ['Harvesting', 'Sowing'],
    preferredLocations: ['Nadiad', 'Anand'],
    minimumDailyWage: 500,
    preferredWorkingHours: '08:00 AM - 05:00 PM',
    foodPreference: 'Preferred',
    accommodationPreference: 'Not Required'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkerPreferencesComponent, NoopAnimationsModule],
      providers: [
        WorkerJobService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerPreferencesComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create component', () => {
    expect(component).toBeTruthy();
  });

  it('should load worker preferences on init', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush(mockPreferences);

    expect(component.loading()).toBe(false);
    expect(component.preferences()).toEqual(mockPreferences);
    expect(component.categories()).toEqual(['Harvesting', 'Sowing']);
    expect(component.locations()).toEqual(['Nadiad', 'Anand']);
    expect(component.prefForm.value.minimumDailyWage).toBe(500);
  });

  it('should handle load error gracefully', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(baseUrl);
    req.flush({ message: 'Error' }, { status: 500, statusText: 'Server Error' });

    expect(component.loading()).toBe(false);
    expect(component.loadError()).toBeTruthy();
  });

  it('should add category and prevent duplicates', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockPreferences);

    // Add new
    component.newCategoryInput.set('Irrigation');
    component.addCategory();
    expect(component.categories()).toContain('Irrigation');
    expect(component.newCategoryInput()).toBe('');

    // Duplicate
    component.newCategoryInput.set('harvesting');
    component.addCategory();
    expect(component.categoryError()).toContain('already added');
  });

  it('should remove category', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockPreferences);

    component.removeCategory(0); // Removes 'Harvesting'
    expect(component.categories()).toEqual(['Sowing']);
  });

  it('should add location and prevent duplicates', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockPreferences);

    component.newLocationInput.set('Kheda');
    component.addLocation();
    expect(component.locations()).toContain('Kheda');

    component.newLocationInput.set('ANAND');
    component.addLocation();
    expect(component.locationError()).toContain('already added');
  });

  it('should remove location', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockPreferences);

    component.removeLocation(0); // Removes 'Nadiad'
    expect(component.locations()).toEqual(['Anand']);
  });

  it('should validate minimum wage non-negative', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockPreferences);

    component.prefForm.patchValue({ minimumDailyWage: -50 });
    expect(component.prefForm.invalid).toBe(true);
  });

  it('should submit updated preferences', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockPreferences);

    component.prefForm.patchValue({ minimumDailyWage: 600 });
    component.onSubmit();

    const putReq = httpMock.expectOne(baseUrl);
    expect(putReq.request.method).toBe('PUT');
    expect(putReq.request.body.minimumDailyWage).toBe(600);

    const updated = { ...mockPreferences, minimumDailyWage: 600 };
    putReq.flush(updated);

    expect(component.saving()).toBe(false);
    expect(component.successMessage()).toContain('saved successfully');
  });

  it('should handle save error', () => {
    fixture.detectChanges();
    httpMock.expectOne(baseUrl).flush(mockPreferences);

    component.onSubmit();
    const putReq = httpMock.expectOne(baseUrl);
    putReq.flush({ message: 'Update failed' }, { status: 400, statusText: 'Bad Request' });

    expect(component.saving()).toBe(false);
  });
});
