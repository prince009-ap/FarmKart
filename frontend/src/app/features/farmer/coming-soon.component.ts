import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-coming-soon',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './coming-soon.component.html'
})
export class ComingSoonComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);

  moduleTitle = signal<string>('Feature Module');

  ngOnInit(): void {
    this.route.data.subscribe(data => {
      if (data['title']) {
        this.moduleTitle.set(data['title']);
      }
    });
  }
}
