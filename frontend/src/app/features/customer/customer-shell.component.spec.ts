import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { CustomerShellComponent } from './customer-shell.component';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';

describe('CustomerShellComponent', () => {
  let fixture: ComponentFixture<CustomerShellComponent>;
  const currentUser$ = new BehaviorSubject<any>(null);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            currentUser$: currentUser$.asObservable(),
            logout: () => undefined,
          },
        },
        {
          provide: NotificationService,
          useValue: {
            getUnreadCount: () => of({ unreadCount: 0 })
          }
        }
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerShellComponent);
    fixture.detectChanges();
  });

  it('renders sidebar navigation items for Customer workspace', () => {
    const navLabels = fixture.componentInstance.navItems.map(item => item.label);
    expect(navLabels).toEqual([
      'Dashboard',
      'Browse Auctions',
      'My Bids',
      'My Orders',
      'Payments',
      'Notifications',
      'My Profile'
    ]);
  });

  it('marks future modules as coming soon', () => {
    const placeholderItems = fixture.componentInstance.navItems.filter(
      (item) => item.isPlaceholder,
    );

    expect(placeholderItems.map((item) => item.label)).toEqual([
      'My Profile'
    ]);
    expect(fixture.nativeElement.textContent).toContain('Soon');
  });

  it('displays authenticated customer name and email from AuthService', () => {
    currentUser$.next({ fullName: 'Priya Sharma', email: 'priya@example.com', role: 'Customer' });
    fixture.detectChanges();

    expect(fixture.componentInstance.userName()).toBe('Priya Sharma');
    expect(fixture.componentInstance.userEmail()).toBe('priya@example.com');
    expect(fixture.nativeElement.textContent).toContain('Priya Sharma');
  });

  it('toggles mobile navigation menu drawer', () => {
    expect(fixture.componentInstance.isMobileMenuOpen()).toBe(false);

    fixture.componentInstance.toggleMobileMenu();
    expect(fixture.componentInstance.isMobileMenuOpen()).toBe(true);

    fixture.componentInstance.closeMobileMenu();
    expect(fixture.componentInstance.isMobileMenuOpen()).toBe(false);
  });
});
