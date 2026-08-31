import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterOutlet, RouterLinkActive } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';

import { FormsModule } from '@angular/forms';
import { LanguageService } from '../../core/services/language.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { environment } from '../../../environments/environment';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  translationKey: string;
  isPlaceholder?: boolean;
}

@Component({
  selector: 'app-farmer-shell',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TranslatePipe,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatMenuModule
  ],
  templateUrl: './farmer-shell.component.html'
})
export class FarmerShellComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  readonly languageService = inject(LanguageService);
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  isMobileMenuOpen = signal(false);
  userName = signal<string>('Farmer');
  userEmail = signal<string>('');
  userAvatarUrl = signal<string | null>(null);
  unreadNotificationsCount = signal<number>(0);

  readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/farmer', icon: 'dashboard', translationKey: 'nav.dashboard' },
    { label: 'Analytics', route: '/farmer/analytics', icon: 'bar_chart', translationKey: 'nav.analytics' },
    { label: 'My Profile', route: '/farmer/profile', icon: 'person', translationKey: 'nav.profile' },
    { label: 'Jobs', route: '/farmer/jobs', icon: 'work', translationKey: 'nav.jobPostings' },
    { label: 'My Crops', route: '/farmer/crops', icon: 'eco', translationKey: 'nav.crops' },
    { label: 'My Auctions', route: '/farmer/auctions', icon: 'gavel', translationKey: 'nav.myAuctions' },
    { label: 'My Orders', route: '/farmer/orders', icon: 'shopping_bag', translationKey: 'nav.orders' },
    { label: 'My Reviews', route: '/farmer/reviews', icon: 'star_rate', translationKey: 'nav.reviews' },
    { label: 'My Machinery', route: '/farmer/machinery', icon: 'construction', translationKey: 'nav.myMachinery' },
    { label: 'My Rentals', route: '/farmer/my-rentals', icon: 'receipt_long', translationKey: 'nav.myRentals' },
    { label: 'My Wishlist', route: '/farmer/wishlist', icon: 'favorite', translationKey: 'nav.wishlist' },
    { label: 'My Reports', route: '/farmer/reports', icon: 'report_problem', translationKey: 'nav.reports' },
    { label: 'My Disputes', route: '/farmer/disputes', icon: 'gavel', translationKey: 'nav.disputes' },
    { label: 'Notifications', route: '/farmer/notifications', icon: 'notifications', translationKey: 'nav.notifications' },
    { label: 'Settings', route: '/farmer/settings', icon: 'settings', translationKey: 'nav.settings' }
  ];

  ngOnInit(): void {
    // Read the current user's profile details from the cached user session
    this.authService.currentUser$.subscribe((user) => {
      if (user) {
        this.userName.set(user.fullName || 'Farmer');
        this.userEmail.set(user.email || '');
        if (user.profileImageUrl) {
          let url = user.profileImageUrl;
          if (url.startsWith('/')) {
            url = `${environment.apiUrl.replace(/\/api$/, '')}${url}`;
          }
          this.userAvatarUrl.set(url);
        } else {
          this.userAvatarUrl.set(null);
        }
      }
    });

    this.notificationService.getUnreadCount().subscribe({
      next: (res) => this.unreadNotificationsCount.set(res.unreadCount),
      error: () => {}
    });
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen.update(val => !val);
  }

  closeMobileMenu(): void {
    this.isMobileMenuOpen.set(false);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/auth/login']);
  }
}
