import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
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
  selector: 'app-customer-shell',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TranslatePipe,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './customer-shell.component.html'
})
export class CustomerShellComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  readonly languageService = inject(LanguageService);
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  isMobileMenuOpen = signal(false);
  userName = signal<string>('Customer');
  userEmail = signal<string>('');
  userAvatarUrl = signal<string | null>(null);
  unreadNotificationsCount = signal<number>(0);

  readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/customer', icon: 'dashboard', translationKey: 'nav.dashboard' },
    { label: 'Analytics', route: '/customer/analytics', icon: 'insights', translationKey: 'nav.analytics' },
    { label: 'Browse Auctions', route: '/customer/auctions', icon: 'gavel', translationKey: 'nav.browseAuctions' },
    { label: 'Rent Machinery', route: '/customer/machinery', icon: 'storefront', translationKey: 'nav.rentMachinery' },
    { label: 'My Machinery', route: '/customer/my-machinery', icon: 'construction', translationKey: 'nav.myMachinery' },
    { label: 'My Rentals', route: '/customer/my-rentals', icon: 'receipt_long', translationKey: 'nav.myRentals' },
    { label: 'My Wishlist', route: '/customer/wishlist', icon: 'favorite', translationKey: 'nav.wishlist' },
    { label: 'My Bids', route: '/customer/bids', icon: 'local_offer', translationKey: 'nav.bids' },
    { label: 'My Orders', route: '/customer/orders', icon: 'shopping_bag', translationKey: 'nav.orders' },
    { label: 'My Reviews', route: '/customer/reviews', icon: 'star_rate', translationKey: 'nav.reviews' },
    { label: 'Payments', route: '/customer/payments', icon: 'payments', translationKey: 'nav.payments' },
    { label: 'Notifications', route: '/customer/notifications', icon: 'notifications', translationKey: 'nav.notifications' },
    { label: 'Settings', route: '/customer/settings', icon: 'settings', translationKey: 'nav.settings' },
    { label: 'My Profile', route: '/customer/profile', icon: 'person', translationKey: 'nav.profile' }
  ];

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userName.set(user.fullName || 'Customer');
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
