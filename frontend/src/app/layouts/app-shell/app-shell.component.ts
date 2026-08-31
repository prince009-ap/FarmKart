import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';

@Component({
  selector: 'app-shell',
  imports: [
    TranslatePipe,MatCardModule, MatChipsModule],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.css',
})
export class AppShellComponent {
  protected readonly roles = ['Farmer', 'Worker', 'Customer'];

  protected readonly plannedModules = [
    'Authentication',
    'Jobs',
    'Machinery',
    'Crops',
    'Marketplace',
    'Auction',
    'Chat',
    'Notifications',
    'AI',
  ];
}
