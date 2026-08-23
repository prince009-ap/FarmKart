import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { LanguageService } from '../../core/services/language.service';

interface CustomerModuleCard {
  titleKey: string;
  descriptionKey: string;
  route: string;
  icon: string;
  status: 'ACTIVE' | 'COMING SOON';
}

@Component({
  selector: 'app-customer-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslatePipe, MatButtonModule, MatIconModule],
  templateUrl: './customer-dashboard.component.html'
})
export class CustomerDashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);
  readonly languageService = inject(LanguageService);

  userName = signal<string>('Customer');

  readonly moduleCards: CustomerModuleCard[] = [
    {
      titleKey: 'nav.browseAuctions',
      descriptionKey: 'customer.dashboardSubtitle',
      route: '/customer/auctions',
      icon: 'gavel',
      status: 'ACTIVE'
    },
    {
      titleKey: 'nav.bids',
      descriptionKey: 'customer.myBidsTitle',
      route: '/customer/bids',
      icon: 'local_offer',
      status: 'ACTIVE'
    },
    {
      titleKey: 'nav.orders',
      descriptionKey: 'customer.myOrdersTitle',
      route: '/customer/orders',
      icon: 'shopping_bag',
      status: 'ACTIVE'
    },
    {
      titleKey: 'nav.payments',
      descriptionKey: 'nav.payments',
      route: '/customer/payments',
      icon: 'payments',
      status: 'ACTIVE'
    },
    {
      titleKey: 'nav.notifications',
      descriptionKey: 'nav.notifications',
      route: '/customer/notifications',
      icon: 'notifications',
      status: 'ACTIVE'
    },
    {
      titleKey: 'nav.profile',
      descriptionKey: 'nav.profile',
      route: '/customer/profile',
      icon: 'person',
      status: 'ACTIVE'
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
