import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { CustomerDashboardComponent } from './customer-dashboard.component';
import { AuthService } from '../../core/services/auth.service';

describe('CustomerDashboardComponent', () => {
  let fixture: ComponentFixture<CustomerDashboardComponent>;
  const currentUser$ = new BehaviorSubject<any>(null);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerDashboardComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: { currentUser$: currentUser$.asObservable() },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerDashboardComponent);
  });

  it('renders customer dashboard with default fallback name', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Welcome, Customer');
    expect(fixture.nativeElement.textContent).toContain('Discover fresh farm produce, explore auctions, and manage your purchases.');
  });

  it('renders authenticated customer name from auth state', () => {
    currentUser$.next({ fullName: 'Rahul Verma', role: 'Customer' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Welcome, Rahul Verma');
  });

  it('renders all required dashboard module cards', () => {
    fixture.detectChanges();

    const titles = fixture.componentInstance.moduleCards.map(card => card.title);
    expect(titles).toEqual([
      'Browse Auctions',
      'My Bids',
      'My Orders',
      'Payments',
      'Notifications',
      'My Profile'
    ]);
  });

  it('correctly sets status badges for active and coming soon cards', () => {
    fixture.detectChanges();

    const activeCard = fixture.componentInstance.moduleCards.find(c => c.title === 'Browse Auctions');
    expect(activeCard?.status).toBe('ACTIVE');

    const soonCards = fixture.componentInstance.moduleCards.filter(c => c.status === 'COMING SOON');
    expect(soonCards.length).toBe(3);
  });
});
