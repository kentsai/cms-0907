import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'admin/publish-statuses',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('@features/publish-statuses/publish-status-list/publish-status-list.component')
            .then(m => m.PublishStatusListComponent)
      },
      // `new` must precede `:id` so it is not captured as an id.
      {
        path: 'new',
        loadComponent: () =>
          import('@features/publish-statuses/publish-status-form/publish-status-form.component')
            .then(m => m.PublishStatusFormComponent)
      },
      {
        path: ':id',
        loadComponent: () =>
          import('@features/publish-statuses/publish-status-detail/publish-status-detail.component')
            .then(m => m.PublishStatusDetailComponent)
      },
      {
        path: ':id/edit',
        loadComponent: () =>
          import('@features/publish-statuses/publish-status-form/publish-status-form.component')
            .then(m => m.PublishStatusFormComponent)
      }
    ]
  }
];
