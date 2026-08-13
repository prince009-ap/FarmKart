import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { FarmerDashboardComponent } from './farmer-dashboard.component';
import { AuthService } from '../../core/services/auth.service';

describe('FarmerDashboardComponent', () => {
  let fixture: ComponentFixture<FarmerDashboardComponent>;
  const currentUser$ = new BehaviorSubject<any>(null);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FarmerDashboardComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: { currentUser$: currentUser$.asObservable() },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerDashboardComponent);
  });

  it('renders the farmer dashboard and safe fallback name', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Welcome back, Farmer!');
  });

  it('renders the authenticated farmer name when available', () => {
    currentUser$.next({ fullName: 'Asha Patel', role: 'Farmer' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Welcome back, Asha Patel!');
  });

  it('links the profile card to the existing farmer profile route', () => {
    fixture.detectChanges();

    const profileCard = fixture.componentInstance.moduleCards.find(
      (card) => card.title === 'My Profile',
    );

    expect(profileCard?.route).toBe('/farmer/profile');
  });
});
