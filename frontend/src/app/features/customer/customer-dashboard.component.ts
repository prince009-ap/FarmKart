import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

interface CustomerModuleCard {
  title: string;
  description: string;
  route: string;
  icon: string;
  status: 'ACTIVE' | 'COMING SOON';
}

@Component({
  selector: 'app-customer-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './customer-dashboard.component.html'
})
export class CustomerDashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);

  userName = signal<string>('Customer');

  readonly moduleCards: CustomerModuleCard[] = [
    {
      title: 'Browse Auctions',
      description: 'Explore fresh produce auctions from local farmers.',
      route: '/customer/auctions',
      icon: 'gavel',
      status: 'ACTIVE'
    },
    {
      title: 'My Bids',
      description: 'Track your active and previous auction bids.',
      route: '/customer/bids',
      icon: 'local_offer',
      status: 'ACTIVE'
    },
    {
      title: 'My Orders',
      description: 'View your completed purchases and order history.',
      route: '/customer/orders',
      icon: 'shopping_bag',
      status: 'COMING SOON'
    },
    {
      title: 'Payments',
      description: 'Manage payments for successful auction purchases.',
      route: '/customer/payments',
      icon: 'payments',
      status: 'ACTIVE'
    },
    {
      title: 'Notifications',
      description: 'Stay updated about bids, auctions, and orders.',
      route: '/customer/notifications',
      icon: 'notifications',
      status: 'COMING SOON'
    },
    {
      title: 'My Profile',
      description: 'Manage your customer account and personal information.',
      route: '/customer/profile',
      icon: 'person',
      status: 'COMING SOON'
    }
  ];

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userName.set(user.fullName || 'Customer');
      }
    });
  }
}
