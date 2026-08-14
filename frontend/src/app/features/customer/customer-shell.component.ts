import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  isPlaceholder?: boolean;
}

@Component({
  selector: 'app-customer-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './customer-shell.component.html'
})
export class CustomerShellComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  isMobileMenuOpen = signal(false);
  userName = signal<string>('Customer');
  userEmail = signal<string>('');

  readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/customer', icon: 'dashboard' },
    { label: 'Browse Auctions', route: '/customer/auctions', icon: 'gavel' },
    { label: 'My Bids', route: '/customer/bids', icon: 'local_offer', isPlaceholder: true },
    { label: 'My Orders', route: '/customer/orders', icon: 'shopping_bag', isPlaceholder: true },
    { label: 'Payments', route: '/customer/payments', icon: 'payments', isPlaceholder: true },
    { label: 'Notifications', route: '/customer/notifications', icon: 'notifications', isPlaceholder: true },
    { label: 'My Profile', route: '/customer/profile', icon: 'person', isPlaceholder: true }
  ];

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userName.set(user.fullName || 'Customer');
        this.userEmail.set(user.email || '');
      }
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
