import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [
    TranslatePipe,RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './unauthorized.component.html'
})
export class UnauthorizedComponent {}
