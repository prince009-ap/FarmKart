import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { FarmerShellComponent } from './farmer-shell.component';
import { AuthService } from '../../core/services/auth.service';

describe('FarmerShellComponent', () => {
  let fixture: ComponentFixture<FarmerShellComponent>;
  const currentUser$ = new BehaviorSubject<any>(null);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FarmerShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            currentUser$: currentUser$.asObservable(),
            logout: () => undefined,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerShellComponent);
    fixture.detectChanges();
  });

  it('contains navigation to the existing farmer profile', () => {
    const profileItem = fixture.componentInstance.navItems.find(
      (item) => item.label === 'My Profile',
    );

    expect(profileItem?.route).toBe('/farmer/profile');
    expect(fixture.nativeElement.textContent).toContain('My Profile');
  });

  it('marks future modules as coming soon rather than treating them as implemented', () => {
    const futureItems = fixture.componentInstance.navItems.filter(
      (item) => item.isPlaceholder,
    );

    expect(futureItems.map((item) => item.label)).toEqual([
      'Jobs',
      'My Crops',
      'Machinery',
      'Marketplace',
      'Notifications',
    ]);
    expect(fixture.nativeElement.textContent).toContain('Soon');
  });

  it('opens and closes the mobile navigation drawer', () => {
    expect(fixture.componentInstance.isMobileMenuOpen()).toBe(false);

    fixture.componentInstance.toggleMobileMenu();
    expect(fixture.componentInstance.isMobileMenuOpen()).toBe(true);

    fixture.componentInstance.closeMobileMenu();
    expect(fixture.componentInstance.isMobileMenuOpen()).toBe(false);
  });
});
